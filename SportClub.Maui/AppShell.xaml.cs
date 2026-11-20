using SportClub.Maui.Services;
using SportClub.Maui.Views;

namespace SportClub.Maui;

public partial class AppShell : Shell
{
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public AppShell(AuthState auth, RealtimeService realtime)
    {
        InitializeComponent();
        _auth = auth;
        _realtime = realtime;

        // Detail pages (opened on top of the tabs)
        Routing.RegisterRoute("session", typeof(SessionDetailPage));
        Routing.RegisterRoute("attendance", typeof(AttendancePage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("buypass", typeof(BuyPassPage));

        // Token expired or logged out → back to login
        _auth.SignedOut += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            _realtime.Stop();
            await GoToAsync("//login");
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _auth.TryRestoreAsync())
        {
            _realtime.Start();
            await GoToAsync(_auth.IsTrainer ? "//sessions" : "//schedule");
        }
    }
}
