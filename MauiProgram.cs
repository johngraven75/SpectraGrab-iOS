using Microsoft.Extensions.Logging;

namespace SpectraGrab;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton<Services.IMobilePersistentConfigService, Services.MobilePersistentConfigService>();
        builder.Services.AddSingleton<Services.IMobileLiveCaptureService, Services.MobileLiveCaptureService>();
        builder.Services.AddSingleton<Services.IMobileAutomatedDownloadService, Services.MobileAutomatedDownloadService>();
        builder.Services.AddSingleton<MainPage>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
