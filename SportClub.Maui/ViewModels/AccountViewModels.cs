using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportClub.Maui.Models;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

public partial class RegisterViewModel : BaseViewModel
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public RegisterViewModel(ApiClient api, AuthState auth, RealtimeService realtime)
    {
        _api = api;
        _auth = auth;
        _realtime = realtime;
    }

    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string confirmPassword = "";

    [RelayCommand]
    private Task RegisterAsync() => RunAsync(async () =>
    {
        // Client-side validation (the API validates the same rules again)
        if (FullName.Trim().Length < 2) { ErrorMessage = "Sisesta oma nimi (vähemalt 2 tähemärki)."; return; }
        if (!EmailRegex.IsMatch(Email.Trim())) { ErrorMessage = "Sisesta korrektne e-posti aadress."; return; }
        if (Password.Length < 8) { ErrorMessage = "Parool peab olema vähemalt 8 tähemärki."; return; }
        if (Password != ConfirmPassword) { ErrorMessage = "Paroolid ei kattu."; return; }

        // API loob konto (roll Client), seejärel logime sama e-posti ja parooliga sisse.
        await _api.RegisterAsync(FullName.Trim(), Email.Trim(), Password);
        var response = await _api.LoginAsync(Email.Trim(), Password);
        await _auth.SignInAsync(response, Email.Trim());
        Password = ConfirmPassword = "";
        _realtime.Start();

        // A new client has no pass yet – take them straight to buying one.
        await Shell.Current.GoToAsync("//pass");
        await Shell.Current.GoToAsync("buypass");
    });

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");
}

/// <summary>One plan card on the buy screen.</summary>
public partial class PlanItem : ObservableObject
{
    private static readonly CultureInfo Et = new("et-EE");

    public PlanItem(PassPlanDto dto) => Dto = dto;

    public PassPlanDto Dto { get; }
    public string Name => Dto.Name;
    public string Description => $"Kehtib {Dto.Days} päeva ostupäevast.";
    public string VisitsText => $"{Dto.Visits} külastust · {Dto.Days} päeva";
    public string PriceText => Dto.Price.ToString("0.00", Et) + " €";
    public string PerVisitText => $"{(Dto.Price / Dto.Visits).ToString("0.00", Et)} € / treening";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(TextColor), nameof(SubTextColor), nameof(StrokeColor))]
    private bool isSelected;

    public Color Background => IsSelected ? Color.FromArgb("#14161A") : Colors.White;
    public Color TextColor => IsSelected ? Color.FromArgb("#F4F5F2") : Color.FromArgb("#14161A");
    public Color SubTextColor => IsSelected ? Color.FromArgb("#B7BCC4") : Color.FromArgb("#5B616B");
    public Color StrokeColor => IsSelected ? Color.FromArgb("#14161A") : Color.FromArgb("#E3E5E0");
}

public partial class BuyPassViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    public BuyPassViewModel(ApiClient api) => _api = api;

    public ObservableCollection<PlanItem> Plans { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuyText), nameof(HasSelection))]
    private PlanItem? selected;

    [ObservableProperty] private string startInfo = "";

    public bool HasSelection => Selected is not null;
    public string BuyText => Selected is null ? "Vali pakett" : $"Osta – {Selected.PriceText}";

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var plans = await _api.GetPassPlansAsync();
        Plans.Clear();
        foreach (var p in plans) Plans.Add(new PlanItem(p));

        // Vaikimisi valitud: 12 külastust või esimene tariif.
        Select(Plans.FirstOrDefault(p => p.Dto.Visits == 12) ?? Plans.FirstOrDefault());

        // Korraga saab olla ainult üks aktiivne kuukaart.
        var current = await _api.GetMyPassAsync();
        StartInfo = current is null
            ? "Kuukaart hakkab kehtima kohe pärast ostu."
            : $"Sul on juba kehtiv kuukaart ({current.VisitsLeft} külastust alles, kuni {current.Until:dd.MM.yyyy}). Uue saad osta, kui see on läbi.";
    });

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void Select(PlanItem? plan)
    {
        foreach (var p in Plans) p.IsSelected = p == plan;
        Selected = plan;
    }

    [RelayCommand]
    private async Task BuyAsync()
    {
        if (Selected is null) return;
        var plan = Selected;

        // Payment is simulated in this school project – no card data is asked or stored.
        var ok = await Shell.Current.DisplayAlertAsync("Kinnita ost",
            $"{plan.Name}\n{plan.VisitsText}\nHind: {plan.PriceText}\n\nMakse on selles demorakenduses simuleeritud – päris raha ei võeta.",
            "Maksa", "Loobu");
        if (!ok) return;

        await RunAsync(async () =>
        {
            var pass = await _api.BuyPassAsync(plan.Dto.Id);
            await ToastAsync("Kuukaart ostetud", $"{plan.Name}: {pass.VisitsTotal} külastust, kehtib kuni {pass.Until:dd.MM.yyyy}. Saad kohe treeninguid broneerida!");
            await Shell.Current.GoToAsync("..");
        });
    }
}
