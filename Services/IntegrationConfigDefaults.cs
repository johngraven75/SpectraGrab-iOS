using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Storage;
using SpectraGrab.Models;

namespace SpectraGrab.Services;

public static class IntegrationConfigDefaults
{
    public const int CurrentSchemaVersion = 2;

    public static IReadOnlyList<IntegrationConfig> ProviderFallbacks { get; } =
    [
        Config("huggingface", "Hugging Face automation", new() { ["endpoint"] = "https://router.huggingface.co/v1/chat/completions", ["model"] = MobileAutomatedDownloadService.Model, ["credentialSecureStorageKey"] = "hf_token", ["credentialRequired"] = false }),
        Config("theporndb", "ThePornDB", new() { ["baseUrl"] = "https://api.theporndb.net", ["credentialSecureStorageKey"] = "tpdb_api_key", ["credentialRequired"] = true, ["posterDownload"] = true }),
        Config("stashdb", "StashDB", new() { ["endpoint"] = "https://stashdb.org/graphql", ["credentialSecureStorageKey"] = "stashdb_api_key", ["credentialRequired"] = true, ["posterDownload"] = true }),
        Config("extractor-metadata", "Extractor metadata", new() { ["source"] = "managed-http", ["credentialRequired"] = false, ["enabledForAllSites"] = true }),
        Config("extractor-thumbnail", "Extractor thumbnail", new() { ["source"] = "managed-http", ["credentialRequired"] = false, ["posterFallback"] = true })
    ];

    public static IReadOnlyList<IntegrationConfig> PluginFallbacks { get; } =
    [
        Config("emby", "Emby", new() { ["baseUrl"] = "http://localhost:8096", ["credentialSecureStorageKey"] = "emby_api_key", ["syncEnabled"] = true, ["enabledAtStartup"] = true }),
        Config("jellyfin", "Jellyfin", new() { ["baseUrl"] = "http://localhost:8096", ["credentialSecureStorageKey"] = "jellyfin_api_key", ["syncEnabled"] = true, ["enabledAtStartup"] = true }),
        Config("plex", "Plex", new() { ["baseUrl"] = "http://localhost:32400", ["credentialSecureStorageKey"] = "plex_token", ["syncEnabled"] = true, ["enabledAtStartup"] = true }),
        Config("localai", "Local AI", new() { ["endpoint"] = "http://localhost:8080", ["model"] = "default", ["enabledForMetadata"] = true, ["enabledAtStartup"] = true }),
        Config("quickconnect", "QuickConnect", new() { ["baseUrl"] = "https://quickconnect.to", ["serverId"] = string.Empty, ["enabledAtStartup"] = true })
    ];

    public static async Task<IReadOnlyList<IntegrationConfig>> LoadPackagedAsync(string kind, IReadOnlyList<IntegrationConfig> fallbacks)
    {
        var configs = new List<IntegrationConfig>(fallbacks.Count);
        foreach (var fallback in fallbacks)
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync($"ConfigDefaults/{kind}/{fallback.Id}.json");
                configs.Add(await JsonSerializer.DeserializeAsync<IntegrationConfig>(stream, JsonOptions) ?? Clone(fallback));
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException)
            {
                configs.Add(Clone(fallback));
            }
        }
        return configs;
    }

    internal static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal static IntegrationConfig Clone(IntegrationConfig config) =>
        JsonSerializer.Deserialize<IntegrationConfig>(JsonSerializer.Serialize(config, JsonOptions), JsonOptions)
        ?? throw new InvalidDataException($"Unable to clone integration configuration {config.Id}.");

    private static IntegrationConfig Config(string id, string name, JsonObject settings) => new() { Id = id, Name = name, Settings = settings };
}