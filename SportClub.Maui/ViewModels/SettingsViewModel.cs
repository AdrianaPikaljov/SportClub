using CommunityToolkit.Mvvm.Input;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

/// <summary>Seaded: keel, teema ja konto. Valik salvestatakse seadmesse (Preferences).</summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public SettingsViewModel(AuthState auth, RealtimeService realtime)
    {
        _auth = auth;
        _realtime = realtime;
    }

    public bool IsEstonian => !Loc.Instance.IsEnglish;
    public bool IsEnglish => Loc.Instance.IsEnglish;
    public bool IsDark => ThemeService.IsDark;

    // Valitud nupp on laimiroheline, teine nagu tavaline kaart.
    public Color EtBackground => Pick(IsEstonian);
    public Color EnBackground => Pick(IsEnglish);
    public Color LightBackground => Pick(!IsDark);
    public Color DarkBackground => Pick(IsDark);
    public Color EtText => PickText(IsEstonian);
    public Color EnText => PickText(IsEnglish);
    public Color LightText => PickText(!IsDark);
    public Color DarkText => PickText(IsDark);

    public string UserName => _auth.User?.Name ?? "";
    public string UserEmail => _auth.User?.Email ?? "";
    public string RoleText => Loc.T(_auth.IsTrainer ? "settings.roleTrainer" : "settings.roleClient");
    public string Initials => string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpperInvariant();

    private static Color Pick(bool selected) => selected ? Color.FromArgb("#D4FF3A") : ThemeService.Get("Surface");
    private static Color PickText(bool selected) => selected ? Color.FromArgb("#14161A") : ThemeService.Get("Ink");

    /// <summary>Uuendab kõik seotud omadused (nt pärast lehele tulekut või vahetust).</summary>
    public void Refresh() => OnPropertyChanged(string.Empty);

    [RelayCommand]
    private void SetLanguage(string lang)
    {
        Loc.Instance.SetLanguage(lang);
        Refresh();
    }

    [RelayCommand]
    private void SetTheme(string theme)
    {
        ThemeService.Apply(theme == "dark");
        Refresh();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _realtime.Stop();
        _auth.SignOut();
        await Shell.Current.GoToAsync("//login");
    }
}
