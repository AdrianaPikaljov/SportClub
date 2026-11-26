using Microsoft.Extensions.Logging;
using SportClub.Maui.Services;
using SportClub.Maui.ViewModels;
using SportClub.Maui.Views;

namespace SportClub.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CrashLog.Init(); // käsitlemata vead → crash.log
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services (singletons: one login state and one WebSocket for the whole app)
        builder.Services.AddSingleton<AuthState>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<RealtimeService>();
        builder.Services.AddSingleton<AppShell>();

        // ViewModels + pages (MVVM, injected through DI)
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<ScheduleViewModel>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<SessionDetailViewModel>();
        builder.Services.AddTransient<SessionDetailPage>();
        builder.Services.AddTransient<BookingsViewModel>();
        builder.Services.AddTransient<BookingsPage>();
        builder.Services.AddTransient<PassViewModel>();
        builder.Services.AddTransient<PassPage>();
        builder.Services.AddTransient<TrainerSessionsViewModel>();
        builder.Services.AddTransient<TrainerSessionsPage>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<AttendancePage>();
        builder.Services.AddTransient<StatsViewModel>();
        builder.Services.AddTransient<StatsPage>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<BuyPassViewModel>();
        builder.Services.AddTransient<BuyPassPage>();

        // Remove the platform's own border/underline from Entry – the design draws its own frame around it.
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoNativeBorder", (handler, view) =>
        {
#if ANDROID
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif WINDOWS
            // Only the border thickness – changing WinUI theme resources here crashed the app on Windows.
            handler.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}