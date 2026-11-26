using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SportClub.Maui.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            // Viga ei sulge enam rakendust: see kirjutatakse crash.log faili ja näidatakse kasutajale.
            UnhandledException += (_, e) =>
            {
                SportClub.Maui.Services.CrashLog.Write(e.Exception, "WinUI");
                e.Handled = true;
                var message = e.Exception?.Message ?? e.Message;
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        if (Microsoft.Maui.Controls.Shell.Current is { } shell)
                            await shell.DisplayAlertAsync("Viga / Error", message, "OK");
                    }
                    catch { /* ignore */ }
                });
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
