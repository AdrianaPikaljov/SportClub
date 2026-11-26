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
        if (FullName.Trim().Length < 2) { ErrorMessage = Loc.T("register.v.name"); return; }
        if (!EmailRegex.IsMatch(Email.Trim())) { ErrorMessage = Loc.T("login.badEmail"); return; }
        if (Password.Length < 8) { ErrorMessage = Loc.T("register.v.password"); return; }
        if (Password != ConfirmPassword) { ErrorMessage = Loc.T("register.v.confirm"); return; }

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
    private Task BackAsync() => GoBackAsync();
}

/// <summary>One plan card on the buy screen.</summary>
public partial class PlanItem : ObservableObject
{
    private static readonly CultureInfo Et = new("et-EE");

    public PlanItem(PassPlanDto dto) => Dto = dto;

    public PassPlanDto Dto { get; }
    public string Name => Dto.Name;
    public string Description => Loc.T("buy.validDays", Dto.Days);
    public string VisitsText => Loc.T("buy.visits", Dto.Visits, Dto.Days);
    public string PriceText => Dto.Price.ToString("0.00", Et) + " €";
    public string PerVisitText => Loc.T("buy.perVisit", (Dto.Price / Dto.Visits).ToString("0.00", Et));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(TextColor), nameof(SubTextColor), nameof(StrokeColor))]
    private bool isSelected;

    public Color Background => IsSelected ? ThemeService.Get("Selected") : ThemeService.Get("Surface");
    public Color TextColor => IsSelected ? ThemeService.Get("OnSelected") : ThemeService.Get("Ink");
    public Color SubTextColor => IsSelected ? ThemeService.Get("SubOnSelected") : ThemeService.Get("Muted");
    public Color StrokeColor => IsSelected ? ThemeService.Get("Selected") : ThemeService.Get("Line");
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

    // Kaardi andmed on ainult mälus: pärast ostu või lehelt lahkumist need tühjendatakse, kuhugi ei salvestata.
    [ObservableProperty] private string cardNumber = "";
    [ObservableProperty] private string cardHolder = "";
    [ObservableProperty] private string expiry = "";
    [ObservableProperty] private string cvc = "";

    public void ClearCard()
    {
        CardNumber = CardHolder = Expiry = Cvc = "";
    }

    public bool HasSelection => Selected is not null;
    public string BuyText => Selected is null ? Loc.T("buy.choose") : Loc.T("buy.button", Selected.PriceText);

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
            ? Loc.T("buy.startNow")
            : Loc.T("buy.hasPass", current.VisitsLeft, current.Until);
    });

    [RelayCommand]
    private Task BackAsync() => GoBackAsync();

    public override void OnPreferencesChanged() => LoadCommand.Execute(null);

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

        // Kliendipoolne kontroll (API kontrollib uuesti)
        var cardError = ValidateCard();
        if (cardError is not null)
        {
            ErrorMessage = cardError;
            return;
        }
        ErrorMessage = "";

        var ok = await Shell.Current.DisplayAlertAsync(Loc.T("buy.confirmTitle"),
            Loc.T("buy.confirmText", plan.Name, plan.VisitsText, plan.PriceText),
            Loc.T("buy.pay"), Loc.T("cancel"));
        if (!ok) return;

        await RunAsync(async () =>
        {
            var pass = await _api.BuyPassAsync(plan.Dto.Id, CardNumber.Trim(), CardHolder.Trim(), Expiry.Trim(), Cvc.Trim());
            ClearCard();
            await ToastAsync(Loc.T("buy.doneTitle"), Loc.T("buy.doneText", plan.Name, pass.VisitsTotal, pass.Until));
            await GoBackAsync();
        });
    }

    private string? ValidateCard()
    {
        var digits = new string(CardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length is < 12 or > 19 || !Luhn(digits)) return Loc.T("card.errNumber");
        if (string.IsNullOrWhiteSpace(CardHolder) || CardHolder.Trim().Length < 2) return Loc.T("card.errHolder");

        var parts = Expiry.Trim().Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var yy)
            || month is < 1 or > 12 || parts[1].Length != 2)
            return Loc.T("card.errExpiry");
        var now = DateTime.Now;
        if (2000 + yy < now.Year || (2000 + yy == now.Year && month < now.Month)) return Loc.T("card.errExpired");

        var cvc = Cvc.Trim();
        if (cvc.Length is < 3 or > 4 || !cvc.All(char.IsDigit)) return Loc.T("card.errCvc");
        return null;
    }

    private static bool Luhn(string digits)
    {
        var sum = 0;
        var dbl = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (dbl && (d *= 2) > 9) d -= 9;
            sum += d;
            dbl = !dbl;
        }
        return sum % 10 == 0;
    }
}
