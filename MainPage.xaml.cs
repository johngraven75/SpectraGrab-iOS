using SpectraGrab.Services;

namespace SpectraGrab;

public partial class MainPage : ContentPage
{
    private readonly IMobileAutomatedDownloadService automation;

    public MainPage(IMobileAutomatedDownloadService automation)
    {
        InitializeComponent();
        this.automation = automation;
    }

    private void OnInspectClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = TryGetHttpUri(out var uri)
            ? $"Ready to inspect: {uri.Host}"
            : "Enter a valid HTTP or HTTPS URL.";
    }

    private async void OnAutomatedDownloadClicked(object? sender, EventArgs e)
    {
        if (!TryGetHttpUri(out var uri))
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

    private bool TryGetHttpUri(out Uri uri)
    {
        if (Uri.TryCreate(UrlEntry.Text?.Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = new Uri("https://localhost");
        return false;
    }
}
