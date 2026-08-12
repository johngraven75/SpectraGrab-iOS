using System.Text.Json.Nodes;
using SpectraGrab.Models;

namespace SpectraGrab.Services;

public static class IntegrationConfigValidator
{
    private static readonly string[] SensitiveNames = ["apiKey", "token", "password", "secret", "credential"];

    public static IntegrationConfig Normalize(IntegrationConfig defaults, IntegrationConfig current)
    {
        if (!IsSafeId(defaults.Id) || !defaults.Id.Equals(current.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Configuration id must match its safe filename id: {defaults.Id}.");
        }
        var normalized = new IntegrationConfig
        {
            SchemaVersion = IntegrationConfigDefaults.CurrentSchemaVersion,
            Id = defaults.Id,
            Name = string.IsNullOrWhiteSpace(current.Name) ? defaults.Name : current.Name,
            Enabled = current.Enabled,
            Version = Math.Max(IntegrationConfigDefaults.CurrentSchemaVersion, current.Version),
            Settings = MergeSettings(defaults.Settings, current.Settings)
        };
        if (string.IsNullOrWhiteSpace(normalized.Name)) throw new InvalidDataException($"Configuration {defaults.Id} is missing its display name.");
        ValidateNode(normalized.Settings, normalized.Id);
        return normalized;
    }

    private static JsonObject MergeSettings(JsonObject defaults, JsonObject? current)
    {
        var merged = (JsonObject)defaults.DeepClone();
        if (current is null) return merged;
        foreach (var (key, value) in current)
        {
            merged[key] = value is JsonObject currentObject && merged[key] is JsonObject defaultObject
                ? MergeSettings(defaultObject, currentObject)
                : value?.DeepClone();
        }
        return merged;
    }

    private static bool IsSafeId(string id) => !string.IsNullOrWhiteSpace(id) && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private static void ValidateNode(JsonNode? node, string configId, string path = "settings")
    {
        if (node is JsonObject obj)
        {
            foreach (var (key, value) in obj)
            {
                var childPath = $"{path}.{key}";
                if (LooksSensitive(key) && !key.Contains("EnvironmentVariable", StringComparison.OrdinalIgnoreCase) && !key.Contains("SecureStorageKey", StringComparison.OrdinalIgnoreCase)
                    && value is JsonValue sensitiveValue && sensitiveValue.TryGetValue<string>(out var secret) && !string.IsNullOrWhiteSpace(secret))
                {
                    throw new InvalidDataException($"Configuration {configId} cannot persist a plaintext secret at {childPath}.");
                }
                if (LooksLikeUrl(key) && value is JsonValue urlValue && urlValue.TryGetValue<string>(out var url) && !string.IsNullOrWhiteSpace(url)
                    && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                {
                    throw new InvalidDataException($"Configuration {configId} contains an invalid HTTP/HTTPS URL at {childPath}.");
                }
                ValidateNode(value, configId, childPath);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++) ValidateNode(array[index], configId, $"{path}[{index}]");
        }
    }

    private static bool LooksSensitive(string key) => SensitiveNames.Any(name => key.Equals(name, StringComparison.OrdinalIgnoreCase) || key.EndsWith(name, StringComparison.OrdinalIgnoreCase));
    private static bool LooksLikeUrl(string key) => key.Equals("endpoint", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Url", StringComparison.OrdinalIgnoreCase);
}