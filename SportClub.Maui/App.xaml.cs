namespace SportClub.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent(); // loads Colors.xaml / AppStyles.xaml first
        // Salvestatud keel ja teema (vaikimisi eesti keel ja tume teema).
        Services.Loc.Instance.Load();
        Services.ThemeService.Load();
        _services = services;
    }

    // AppShell is created only here, after the app resources exist.
    // (If it were injected into the constructor, it would be built before InitializeComponent
    // and StaticResource lookups like {StaticResource Ink} would fail.)
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<AppShell>());
}
