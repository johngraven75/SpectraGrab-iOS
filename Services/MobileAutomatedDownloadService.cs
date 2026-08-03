using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpectraGrab.Models;

namespace SpectraGrab.Services;

public sealed record MobileDownloadResult(
    string Status,
    string FilePath,
    string? PosterPath,
    string NfoPath,
    bool AdultMedia,
    string AiModel,
    string AiPlan,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> Warnings);

public interface IMobileAutomatedDownloadService
{
    Task<MobileDownloadResult> DownloadAsync(string url, IProgress<double>? progress, CancellationToken cancellationToken);
}

public sealed class MobileAutomatedDownloadService : IMobileAutomatedDownloadService
{
    public const string Model = "Qwen/Qwen3-4B-Instruct-2507";
    private const long MaxPosterBytes = 25L * 1024L * 1024L;
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(20) };
    private readonly IMobilePersistentConfigService persistentConfigs;

    public MobileAutomatedDownloadService(IMobilePersistentConfigService persistentConfigs)
    {
        this.persistentConfigs = persistentConfigs;
    }

    public async Task<MobileDownloadResult> DownloadAsync(string url, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await persistentConfigs.EnsureInitializedAsync();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter a full HTTP or HTTPS media URL.", nameof(url));
        }

        var plan = await PlanAsync(url, cancellationToken);
        var outputDirectory = Path.Combine(FileSystem.AppDataDirectory, "Downloads");
        Directory.CreateDirectory(outputDirectory);
        var safeName = Sanitize(Path.GetFileNameWithoutExtension(uri.AbsolutePath));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "SpectraGrab-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        }

        var filePath = uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
            ? await DownloadHlsAsync(uri, outputDirectory, safeName, progress, cancellationToken)
            : await DownloadDirectAsync(uri, outputDirectory, safeName, progress, cancellationToken);

        var adult = IsAdult(url) || IsAdult(filePath);
        var providers = new List<string>();
        var warnings = new List<string>();
        var metadata = new Metadata(safeName, null, null, null, adult ? "Adult" : "Video", null);
        if (adult)
        {
            var tpdbConfig = persistentConfigs.LoadProviderConfig("theporndb");
            var tpdbKey = await SecureStorage.Default.GetAsync(Setting(tpdbConfig, "credentialSecureStorageKey", "tpdb_api_key"));
            if (tpdbConfig.Enabled && !string.IsNullOrWhiteSpace(tpdbKey))
            {
                providers.Add("ThePornDB");
                try { metadata = Merge(await FetchTpdbAsync(safeName, tpdbKey, Setting(tpdbConfig, "baseUrl", "https://api.theporndb.net"), cancellationToken), metadata); }
                catch (Exception ex) { warnings.Add("ThePornDB: " + ex.Message); }
            }

            var stashConfig = persistentConfigs.LoadProviderConfig("stashdb");
            var stashKey = await SecureStorage.Default.GetAsync(Setting(stashConfig, "credentialSecureStorageKey", "stashdb_api_key"));
            if (stashConfig.Enabled && !string.IsNullOrWhiteSpace(stashKey))
            {
                providers.Add("StashDB");
                try { metadata = Merge(await FetchStashAsync(safeName, stashKey, Setting(stashConfig, "endpoint", "https://stashdb.org/graphql"), cancellationToken), metadata); }
                catch (Exception ex) { warnings.Add("StashDB: " + ex.Message); }
            }
        }

        string? posterPath = null;
        if (!string.IsNullOrWhiteSpace(metadata.PosterUrl))
        {
            try { posterPath = await DownloadPosterAsync(metadata.PosterUrl, filePath, cancellationToken); }
            catch (Exception ex) { warnings.Add("Poster: " + ex.Message); }
        }

        var nfoPath = WriteNfo(filePath, metadata with { PosterUrl = posterPath });
        return new(
            warnings.Count == 0 ? "verified" : "verified_with_warnings",
            filePath,
            posterPath,
            nfoPath,
            adult,
            Model,
            plan,
            providers,
            warnings);
    }

    private async Task<string> PlanAsync(string url, CancellationToken token)
    {
        var fallback = JsonSerializer.Serialize(new
        {
            mediaKind = url.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? "hls" : "direct",
            likelyAdult = IsAdult(url),
            metadata = "provider_then_filename",
            poster = "provider_then_none",
            safety = "no_drm_paywall_access_control_or_captcha_bypass"
        });
        var hfConfig = persistentConfigs.LoadProviderConfig("huggingface");
        var hfToken = await SecureStorage.Default.GetAsync(Setting(hfConfig, "credentialSecureStorageKey", "hf_token"));
        if (!hfConfig.Enabled || string.IsNullOrWhiteSpace(hfToken))
        {
            return fallback;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Setting(hfConfig, "endpoint", "https://router.huggingface.co/v1/chat/completions"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", hfToken);
            request.Content = JsonContent.Create(new
            {
                model = Setting(hfConfig, "model", Model),
                messages = new object[]
                {
                    new { role = "system", content = "Return compact JSON for lawful media download planning. Never suggest bypassing DRM, paywalls, access controls, credentials, or CAPTCHAs." },
                    new { role = "user", content = url }
                },
                temperature = 0.1,
                max_tokens = 180
            });
            using var response = await client.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) return fallback;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? fallback;
        }
        catch { return fallback; }
    }

    private async Task<string> DownloadDirectAsync(Uri uri, string directory, string name, IProgress<double>? progress, CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        var extension = Path.GetExtension(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 8)
        {
            extension = response.Content.Headers.ContentType?.MediaType switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "video/quicktime" => ".mov",
                _ => ".mp4"
            };
        }

        var destination = UniquePath(directory, name, extension);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(destination);
        var total = response.Content.Headers.ContentLength;
        var buffer = new byte[128 * 1024];
        long written = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            written += read;
            if (total is > 0) progress?.Report(written * 100d / total.Value);
        }
        progress?.Report(100);
        return destination;
    }

    private async Task<string> DownloadHlsAsync(Uri manifestUri, string directory, string name, IProgress<double>? progress, CancellationToken token)
    {
        var manifest = await client.GetStringAsync(manifestUri, token);
        if (manifest.Contains("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase)
            && !manifest.Contains("METHOD=NONE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Encrypted/DRM HLS is not downloaded. SpectraGrab does not bypass content protection.");
        }

        var lines = manifest.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        if (lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase)))
        {
            var variants = lines.Where(line => !line.StartsWith('#')).ToList();
            if (variants.Count == 0) throw new InvalidOperationException("HLS master playlist contains no playable variants.");
            manifestUri = new Uri(manifestUri, variants.Last());
            manifest = await client.GetStringAsync(manifestUri, token);
            lines = manifest.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
        }

        var segments = lines.Where(line => !line.StartsWith('#')).Select(line => new Uri(manifestUri, line)).ToList();
        if (segments.Count == 0) throw new InvalidOperationException("HLS playlist contains no media segments.");
        var destination = UniquePath(directory, name, ".ts");
        await using var output = File.Create(destination);
        for (var index = 0; index < segments.Count; index++)
        {
            var bytes = await client.GetByteArrayAsync(segments[index], token);
            await output.WriteAsync(bytes, token);
            progress?.Report((index + 1) * 100d / segments.Count);
        }
        return destination;
    }

    private async Task<Metadata> FetchTpdbAsync(string title, string key, string baseUrl, CancellationToken token)
    {
        baseUrl = baseUrl.TrimEnd('/');
        using var searchRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/scenes?parse={Uri.EscapeDataString(title)}&hash=&year=");
        searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var searchResponse = await client.SendAsync(searchRequest, token);
        searchResponse.EnsureSuccessStatusCode();
        using var search = JsonDocument.Parse(await searchResponse.Content.ReadAsStringAsync(token));
        var first = search.RootElement.GetProperty("data").EnumerateArray().FirstOrDefault();
        var id = Get(first, "uuid") ?? Get(first, "UUID");
        if (string.IsNullOrWhiteSpace(id)) return Metadata.Empty;

        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/scenes/{id}");
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var detailResponse = await client.SendAsync(detailRequest, token);
        detailResponse.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await detailResponse.Content.ReadAsStringAsync(token));
        var detail = document.RootElement.TryGetProperty("data", out var data) ? data : document.RootElement;
        return new(Get(detail, "title"), Get(detail, "description") ?? Get(detail, "details"), Nested(detail, "posters", "large") ?? Get(detail, "poster"), Year(Get(detail, "date")), Tags(detail), Get(detail, "uuid"));
    }

    private async Task<Metadata> FetchStashAsync(string title, string key, string endpoint, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("ApiKey", key);
        request.Content = JsonContent.Create(new
        {
            query = "query($title:String!){ queryScenes(input:{title:$title, per_page:1, page:1, direction:DESC, sort:DATE}) { scenes { title details release_date images { url } tags { name } } } }",
            variables = new { title }
        });
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var scene = document.RootElement.GetProperty("data").GetProperty("queryScenes").GetProperty("scenes").EnumerateArray().FirstOrDefault();
        if (scene.ValueKind != JsonValueKind.Object) return Metadata.Empty;
        var poster = scene.TryGetProperty("images", out var images) ? images.EnumerateArray().Select(image => Get(image, "url")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) : null;
        return new(Get(scene, "title"), Get(scene, "details"), poster, Year(Get(scene, "release_date")), Tags(scene), null);
    }

    private async Task<string> DownloadPosterAsync(string url, string mediaPath, CancellationToken token)
    {
        var bytes = await client.GetByteArrayAsync(url, token);
        if (bytes.LongLength > MaxPosterBytes || !IsImage(bytes)) throw new InvalidOperationException("provider response is not a supported image");
        var ext = bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }) ? ".png" : ".jpg";
        var path = Path.Combine(Path.GetDirectoryName(mediaPath)!, Path.GetFileNameWithoutExtension(mediaPath) + "-poster" + ext);
        await File.WriteAllBytesAsync(path, bytes, token);
        return path;
    }

    private static string WriteNfo(string mediaPath, Metadata metadata)
    {
        var path = Path.ChangeExtension(mediaPath, ".nfo");
        var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<movie>\n  <title>{Escape(metadata.Title ?? Path.GetFileNameWithoutExtension(mediaPath))}</title>\n  <year>{metadata.Year?.ToString() ?? ""}</year>\n  <plot>{Escape(metadata.Overview)}</plot>\n  <genre>{Escape(metadata.Genre)}</genre>\n  <uniqueid type=\"adult-provider\">{Escape(metadata.ProviderId)}</uniqueid>\n  <thumb>{Escape(metadata.PosterUrl)}</thumb>\n</movie>\n";
        File.WriteAllText(path, xml, new UTF8Encoding(false));
        return path;
    }

    private static Metadata Merge(Metadata incoming, Metadata fallback) => new(incoming.Title ?? fallback.Title, incoming.Overview ?? fallback.Overview, incoming.PosterUrl ?? fallback.PosterUrl, incoming.Year ?? fallback.Year, incoming.Genre ?? fallback.Genre, incoming.ProviderId ?? fallback.ProviderId);
    private static string UniquePath(string directory, string name, string extension) { var candidate = Path.Combine(directory, name + extension); for (var i = 1; File.Exists(candidate); i++) candidate = Path.Combine(directory, $"{name} ({i}){extension}"); return candidate; }
    private static string Sanitize(string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
    private static bool IsAdult(string value) { var lower = value.ToLowerInvariant(); return new[] { "adult", "porn", "xxx", "nsfw", "xvideos", "xnxx", "pornhub", "redtube", "youporn", "xhamster", "spankbang", "boyfriend.tv" }.Any(lower.Contains); }
    private static bool IsImage(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) || bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
    private static string? Get(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String ? node.GetString() : null;
    private static string? Nested(JsonElement value, string parent, string child) => value.TryGetProperty(parent, out var nested) ? Get(nested, child) : null;
    private static int? Year(string? value) => value is { Length: >= 4 } && int.TryParse(value[..4], out var year) ? year : null;
    private static string? Tags(JsonElement value) => value.TryGetProperty("tags", out var tags) ? string.Join(", ", tags.EnumerateArray().Select(tag => Get(tag, "name")).Where(name => !string.IsNullOrWhiteSpace(name)).Take(8)) : null;
    private static string Escape(string? value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
    private static string Setting(IntegrationConfig config, string key, string fallback) =>
        config.Settings[key] is JsonValue value && value.TryGetValue<string>(out var setting) && !string.IsNullOrWhiteSpace(setting)
            ? setting
            : fallback;
    private sealed record Metadata(string? Title, string? Overview, string? PosterUrl, int? Year, string? Genre, string? ProviderId) { public static Metadata Empty { get; } = new(null, null, null, null, null, null); }
}
