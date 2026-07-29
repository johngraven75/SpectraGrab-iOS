namespace SpectraGrab;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnInspectClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = TryGetHttpUri(out var uri)
            ? $"Ready to inspect: {uri.Host}"
            : "Enter a valid HTTP or HTTPS URL.";
    }

    private void OnQueueClicked(object? sender, EventArgs e)
    {
        StatusLabel.Text = TryGetHttpUri(out var uri)
            ? $"Queued: {uri.AbsoluteUri}"
            : "Enter a valid HTTP or HTTPS URL before queuing.";
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
