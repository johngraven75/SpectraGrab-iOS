using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace SpectraGrab.Services;

public sealed record MobileCaptureProgress(string OutputPath, TimeSpan Elapsed, long BytesWritten, string Status);
public sealed record MobileCaptureResult(string OutputPath, TimeSpan Duration, long BytesWritten, bool StoppedByUser);

public interface IMobileLiveCaptureService
{
    Task<MobileCaptureResult> CaptureAsync(string url, IProgress<MobileCaptureProgress>? progress, CancellationToken cancellationToken);
}

public sealed class MobileLiveCaptureService : IMobileLiveCaptureService
{
    private readonly HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task<MobileCaptureResult> CaptureAsync(
        string url,
        IProgress<MobileCaptureProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter a full HTTP or HTTPS live-stream URL.", nameof(url));
        }

        var directory = Path.Combine(FileSystem.AppDataDirectory, "Captures");
        Directory.CreateDirectory(directory);
        var isHls = uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        var outputPath = UniquePath(directory, $"SpectraGrab-Capture-{DateTime.UtcNow:yyyyMMdd-HHmmss}", isHls ? ".ts" : MediaExtension(uri));
        var stopwatch = Stopwatch.StartNew();
        long bytesWritten = 0;
        var stopped = false;

        await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 128 * 1024, true);
        try
        {
            if (isHls)
            {
                bytesWritten = await CaptureHlsAsync(uri, output, stopwatch, outputPath, progress, cancellationToken);
            }
            else
            {
                bytesWritten = await CaptureDirectAsync(uri, output, stopwatch, outputPath, progress, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopped = true;
        }
        finally
        {
            await output.FlushAsync(CancellationToken.None);
            bytesWritten = output.Length;
            stopwatch.Stop();
        }

        progress?.Report(new(outputPath, stopwatch.Elapsed, bytesWritten, stopped ? "Stopped and finalized" : "Complete"));
        return new(outputPath, stopwatch.Elapsed, bytesWritten, stopped);
    }

    private async Task<long> CaptureHlsAsync(
        Uri manifestUri,
        Stream output,
        Stopwatch stopwatch,
        string outputPath,
        IProgress<MobileCaptureProgress>? progress,
        CancellationToken token)
    {
        var capturedSegments = new HashSet<string>(StringComparer.Ordinal);
        long bytesWritten = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var manifest = await client.GetStringAsync(manifestUri, token);
            var lines = Lines(manifest);
            if (lines.Any(line => line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase)))
            {
                var variant = lines.LastOrDefault(line => !line.StartsWith('#'))
                    ?? throw new InvalidOperationException("HLS master playlist contains no playable variants.");
                manifestUri = new Uri(manifestUri, variant);
                continue;
            }
            if (lines.Any(line => line.StartsWith("#EXT-X-KEY", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("METHOD=NONE", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Encrypted/DRM HLS is not captured. SpectraGrab does not bypass content protection.");
            }
            if (lines.Any(line => line.StartsWith("#EXT-X-BYTERANGE", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Byte-range HLS capture is not supported by the mobile capture engine.");
            }

            foreach (var line in lines.Where(line => !line.StartsWith('#')))
            {
                var segmentUri = new Uri(manifestUri, line);
                if (!capturedSegments.Add(segmentUri.AbsoluteUri)) continue;
                using var response = await client.GetAsync(segmentUri, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(token);
                bytesWritten += await CopyToCaptureAsync(input, output, bytesWritten, stopwatch, outputPath, progress, token);
            }

            if (lines.Any(line => line.Equals("#EXT-X-ENDLIST", StringComparison.OrdinalIgnoreCase))) break;
            await Task.Delay(RefreshDelay(lines), token);
        }
        return bytesWritten;
    }

    private async Task<long> CaptureDirectAsync(
        Uri uri,
        Stream output,
        Stopwatch stopwatch,
        string outputPath,
        IProgress<MobileCaptureProgress>? progress,
        CancellationToken token)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        return await CopyToCaptureAsync(input, output, 0, stopwatch, outputPath, progress, token);
    }

    private static async Task<long> CopyToCaptureAsync(
        Stream input,
        Stream output,
        long existingBytes,
        Stopwatch stopwatch,
        string outputPath,
        IProgress<MobileCaptureProgress>? progress,
        CancellationToken token)
    {
        var buffer = new byte[128 * 1024];
        long copied = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            copied += read;
            progress?.Report(new(outputPath, stopwatch.Elapsed, existingBytes + copied, "Capturing"));
        }
        return copied;
    }

    private static List<string> Lines(string manifest) => manifest.Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.Length > 0)
        .ToList();

    private static TimeSpan RefreshDelay(IReadOnlyList<string> lines)
    {
        var targetLine = lines.FirstOrDefault(line => line.StartsWith("#EXT-X-TARGETDURATION:", StringComparison.OrdinalIgnoreCase));
        return targetLine is not null
            && double.TryParse(targetLine[(targetLine.IndexOf(':') + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds / 2, 1, 6))
            : TimeSpan.FromSeconds(2);
    }

    private static string MediaExtension(Uri uri)
    {
        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrWhiteSpace(extension) || extension.Length > 8 ? ".mp4" : extension;
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        var candidate = Path.Combine(directory, name + extension);
        for (var index = 1; File.Exists(candidate); index++) candidate = Path.Combine(directory, $"{name}-{index}{extension}");
        return candidate;
    }
}
