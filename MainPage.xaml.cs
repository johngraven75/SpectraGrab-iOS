using Microsoft.Maui.ApplicationModel;
using SpectraGrab.Services;

namespace SpectraGrab;

public partial class MainPage : ContentPage
{
    private readonly IMobileAutomatedDownloadService automation;
    private readonly IMobileLiveCaptureService captureService;
    private readonly IMobilePersistentConfigService persistentConfigs;
    private CancellationTokenSource? captureCancellation;
    private bool configsInitialized;

    public MainPage(
        IMobileAutomatedDownloadService automation,
        IMobileLiveCaptureService captureService,
        IMobilePersistentConfigService persistentConfigs)
    {
        InitializeComponent();
        this.automation = automation;
        this.captureService = captureService;
        this.persistentConfigs = persistentConfigs;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (configsInitialized) return;
        configsInitialized = true;
        try
        {
            var report = await persistentConfigs.EnsureInitializedAsync();
            ConfigStatusLabel.Text = $"Verified {report.ProviderCount} providers and {report.PluginCount} add-ins. Settings survive app upgrades.";
        }
        catch (Exception ex)
        {
            configsInitialized = false;
            ConfigStatusLabel.Text = $"Configuration verification failed: {ex.Message}";
        }
    }

    private void OnInspectClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = TryGetHttpUri(UrlEntry.Text, out var uri)
            ? $"Ready to inspect: {uri.Host}"
            : "Enter a valid HTTP or HTTPS URL.";
    }

    private async void OnAutomatedDownloadClicked(object? sender, EventArgs e)
    {
        if (!TryGetHttpUri(UrlEntry.Text, out var uri))
        {
            StatusLabel.Text = "Enter a valid HTTP or HTTPS URL before downloading.";
            return;
        }

        DownloadButton.IsEnabled = false;
        DownloadProgress.Progress = 0;
        StatusLabel.Text = "AI is planning, downloading, and organizing the media...";
        try
        {
            var progress = new Progress<double>(value =>
                MainThread.BeginInvokeOnMainThread(() => DownloadProgress.Progress = Math.Clamp(value, 0, 100) / 100d));
            var result = await automation.DownloadAsync(uri.AbsoluteUri, progress, CancellationToken.None);
            StatusLabel.Text = result.Warnings.Count == 0
                ? $"Complete: metadata and poster verified. Saved to {result.FilePath}"
                : $"Complete with warnings: {string.Join("; ", result.Warnings)}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }

    private async void OnCaptureClicked(object? sender, EventArgs e)
    {
        if (captureCancellation is not null) return;
        if (!TryGetHttpUri(CaptureUrlEntry.Text, out var uri))
        {
            CaptureStatusLabel.Text = "Enter a valid HTTP or HTTPS live-stream URL.";
            return;
        }

        captureCancellation = new CancellationTokenSource();
        CaptureButton.IsEnabled = false;
        StopCaptureButton.IsEnabled = true;
        CaptureStatusLabel.Text = "Starting capture...";
        try
        {
            var progress = new Progress<MobileCaptureProgress>(update =>
                MainThread.BeginInvokeOnMainThread(() =>
                    CaptureStatusLabel.Text = $"{update.Status} • {update.Elapsed:hh\\:mm\\:ss} • {FormatBytes(update.BytesWritten)}"));
            var result = await captureService.CaptureAsync(uri.AbsoluteUri, progress, captureCancellation.Token);
            CaptureStatusLabel.Text = result.StoppedByUser
                ? $"Stopped and finalized {FormatBytes(result.BytesWritten)} at {result.OutputPath}"
                : $"Capture complete: {FormatBytes(result.BytesWritten)} at {result.OutputPath}";
        }
        catch (Exception ex)
        {
            CaptureStatusLabel.Text = ex.Message;
        }
        finally
        {
            captureCancellation.Dispose();
            captureCancellation = null;
            CaptureButton.IsEnabled = true;
            StopCaptureButton.IsEnabled = false;
        }
    }

    private void OnStopCaptureClicked(object? sender, EventArgs e)
    {
        CaptureStatusLabel.Text = "Stopping and finalizing capture...";
        captureCancellation?.Cancel();
    }

    private static bool TryGetHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = new Uri("https://localhost");
        return false;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
