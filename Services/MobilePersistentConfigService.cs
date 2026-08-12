using System.Text.Json;
using Microsoft.Maui.Storage;
using SpectraGrab.Models;

namespace SpectraGrab.Services;

public interface IMobilePersistentConfigService
{
    string Status { get; }
    Task<IntegrationConfigReport> EnsureInitializedAsync();
    IntegrationConfig LoadProviderConfig(string id);
    IntegrationConfig LoadPluginConfig(string id);
    Task SaveProviderConfigAsync(IntegrationConfig config);
    Task SavePluginConfigAsync(IntegrationConfig config);
}

public sealed class MobilePersistentConfigService : IMobilePersistentConfigService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string configRoot = Path.Combine(FileSystem.AppDataDirectory, "config");
    private List<IntegrationConfig> providers = [];
    private List<IntegrationConfig> plugins = [];
    private IntegrationConfigReport? lastReport;

    public string Status { get; private set; } = "Persistent integration configuration has not been initialized.";

    public async Task<IntegrationConfigReport> EnsureInitializedAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lastReport is not null) return lastReport;
            var providerDirectory = Path.Combine(configRoot, "providers");
            var pluginDirectory = Path.Combine(configRoot, "plugins");
            Directory.CreateDirectory(providerDirectory);
            Directory.CreateDirectory(pluginDirectory);
            var providerDefaults = await IntegrationConfigDefaults.LoadPackagedAsync("providers", IntegrationConfigDefaults.ProviderFallbacks);
            var pluginDefaults = await IntegrationConfigDefaults.LoadPackagedAsync("plugins", IntegrationConfigDefaults.PluginFallbacks);
            var counters = new ConfigCounters();
            providers = [];
            foreach (var defaults in providerDefaults) providers.Add(await LoadAsync(providerDirectory, defaults, counters));
            plugins = [];
            foreach (var defaults in pluginDefaults) plugins.Add(await LoadAsync(pluginDirectory, defaults, counters));
            providers = providers.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();
            plugins = plugins.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase).ToList();
            lastReport = new(configRoot, providers.Count, plugins.Count, counters.Created, counters.Upgraded, counters.Repaired);
            Status = $"Verified {providers.Count} provider and {plugins.Count} add-in configs. Created {counters.Created}, upgraded {counters.Upgraded}, repaired {counters.Repaired}.";
            return lastReport;
        }
        catch (Exception ex)
        {
            Status = $"Persistent integration configuration verification failed: {ex.Message}";
            throw;
        }
        finally { gate.Release(); }
    }

    public IntegrationConfig LoadProviderConfig(string id) => Load(providers, id, "provider");
    public IntegrationConfig LoadPluginConfig(string id) => Load(plugins, id, "add-in");
    public Task SaveProviderConfigAsync(IntegrationConfig config) => SaveAsync("providers", providers, IntegrationConfigDefaults.ProviderFallbacks, config);
    public Task SavePluginConfigAsync(IntegrationConfig config) => SaveAsync("plugins", plugins, IntegrationConfigDefaults.PluginFallbacks, config);

    private async Task SaveAsync(string kind, List<IntegrationConfig> items, IReadOnlyList<IntegrationConfig> fallbacks, IntegrationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        await EnsureInitializedAsync();
        await gate.WaitAsync();
        try
        {
            var defaults = fallbacks.FirstOrDefault(item => item.Id.Equals(config.Id, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Unknown {kind} configuration: {config.Id}.");
            var normalized = IntegrationConfigValidator.Normalize(defaults, config);
            await AtomicWriteAsync(Path.Combine(configRoot, kind, $"{normalized.Id}.json"), normalized);
            var index = items.FindIndex(item => item.Id.Equals(normalized.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException($"Unknown {kind} configuration: {config.Id}.");
            items[index] = normalized;
        }
        finally { gate.Release(); }
    }

    private static IntegrationConfig Load(List<IntegrationConfig> items, string id, string kind)
    {
        var config = items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Persistent configuration must be initialized before loading {kind} {id}.");
        return IntegrationConfigDefaults.Clone(config);
    }

    private static async Task<IntegrationConfig> LoadAsync(string directory, IntegrationConfig defaults, ConfigCounters counters)
    {
        var path = Path.Combine(directory, $"{defaults.Id}.json");
        if (!File.Exists(path))
        {
            var created = IntegrationConfigValidator.Normalize(defaults, defaults);
            await AtomicWriteAsync(path, created);
            counters.Created++;
            return created;
        }
        try
        {
            var original = await File.ReadAllTextAsync(path);
            var current = JsonSerializer.Deserialize<IntegrationConfig>(original, IntegrationConfigDefaults.JsonOptions)
                ?? throw new InvalidDataException($"Configuration {defaults.Id} is empty.");
            var normalized = IntegrationConfigValidator.Normalize(defaults, current);
            var serialized = Serialize(normalized);
            if (!JsonEquivalent(original, serialized))
            {
                await AtomicWriteAsync(path, normalized);
                counters.Upgraded++;
            }
            return normalized;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            File.Move(path, $"{path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
            var repaired = IntegrationConfigValidator.Normalize(defaults, defaults);
            await AtomicWriteAsync(path, repaired);
            counters.Repaired++;
            return repaired;
        }
    }

    private static async Task AtomicWriteAsync(string path, IntegrationConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Cannot resolve configuration directory for {path}."));
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, Serialize(config));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static string Serialize(IntegrationConfig value) => JsonSerializer.Serialize(value, IntegrationConfigDefaults.JsonOptions) + Environment.NewLine;
    private static bool JsonEquivalent(string left, string right)
    {
        using var leftDocument = JsonDocument.Parse(left);
        using var rightDocument = JsonDocument.Parse(right);
        return JsonSerializer.Serialize(leftDocument.RootElement).Equals(JsonSerializer.Serialize(rightDocument.RootElement), StringComparison.Ordinal);
    }
    private sealed class ConfigCounters { public int Created { get; set; } public int Upgraded { get; set; } public int Repaired { get; set; } }
}