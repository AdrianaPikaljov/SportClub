using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportClub.Maui.Models;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

public partial class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public LoginViewModel(ApiClient api, AuthState auth, RealtimeService realtime)
    {
        _api = api;
        _auth = auth;
        _realtime = realtime;
    }

    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";

    [RelayCommand]
    private Task LoginAsync() => RunAsync(async () =>
    {
        // client-side validation
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Sisesta korrektne e-posti aadress.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Sisesta parool.";
            return;
        }

        var response = await _api.LoginAsync(Email.Trim(), Password);
        if (response.Role is not ("Client" or "Trainer"))
        {
            ErrorMessage = "Mobiilirakendus on klientidele ja treeneritele. Admin kasutab veebiliidest.";
            return;
        }
        await _auth.SignInAsync(response, Email.Trim());
        Password = "";
        _realtime.Start();
        await Shell.Current.GoToAsync(_auth.IsTrainer ? "//sessions" : "//schedule");
    });

    [RelayCommand]
    private Task GoToRegisterAsync() => Shell.Current.GoToAsync("register");
}

public partial class ScheduleViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly RealtimeService _realtime;
    private bool _typesLoaded;

    public ScheduleViewModel(ApiClient api, RealtimeService realtime)
    {
        _api = api;
        _realtime = realtime;
        var today = DateTime.Today;
        for (var i = 0; i < 7; i++) Days.Add(new DayItem(today.AddDays(i)) { IsSelected = i == 0 });
        Types.Add(new ChipItem(null, "Kõik") { IsSelected = true });

        _realtime.FreeSpotsChanged += (_, m) =>
        {
            var item = Sessions.FirstOrDefault(s => s.Id == m.SessionId);
            if (item is not null) item.FreeSpots = m.Free;
        };
        _realtime.ConnectionChanged += (_, connected) => IsLive = connected;
        isLive = _realtime.IsConnected;
    }

    public ObservableCollection<DayItem> Days { get; } = new();
    public ObservableCollection<ChipItem> Types { get; } = new();
    public ObservableCollection<SessionItem> Sessions { get; } = new();

    [ObservableProperty] private bool isLive;
    [ObservableProperty] private bool isEmpty;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        if (!_typesLoaded)
        {
            var types = await _api.GetTrainingTypesAsync();
            foreach (var t in types) Types.Add(new ChipItem(t.Name, t.Name));
            _typesLoaded = true;
        }

        var day = Days.First(d => d.IsSelected).Date;
        var type = Types.First(t => t.IsSelected).Type;
        var list = await _api.GetSessionsAsync(day, day, type);

        Sessions.Clear();
        // Tänasest päevast näitame ainult neid, mis pole veel alanud.
        foreach (var s in list.Where(s => s.Start > DateTime.Now)) Sessions.Add(new SessionItem(s));
        IsEmpty = Sessions.Count == 0;
    });

    [RelayCommand]
    private async Task SelectDayAsync(DayItem day)
    {
        foreach (var d in Days) d.IsSelected = d == day;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SelectTypeAsync(ChipItem chip)
    {
        foreach (var t in Types) t.IsSelected = t == chip;
        await LoadAsync();
    }

    [RelayCommand]
    private Task OpenAsync(SessionItem item) =>
        Shell.Current.GoToAsync("session", new Dictionary<string, object> { ["Session"] = item.Dto });
}

public partial class SessionDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private SessionDto? _dto;

    public SessionDetailViewModel(ApiClient api, RealtimeService realtime)
    {
        _api = api;
        realtime.FreeSpotsChanged += (_, m) =>
        {
            if (Session is not null && m.SessionId == Session.Id) Session.FreeSpots = m.Free;
            OnPropertyChanged(nameof(Progress));
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Progress), nameof(When))]
    private SessionItem? session;

    [ObservableProperty] private string passInfo = "";
    [ObservableProperty] private bool isBooked;
    [ObservableProperty] private string bookedLabel = "";
    [ObservableProperty] private bool hasNoPass;

    public string When => Session is null ? "" :
        $"{Session.Dto.Start:ddd, dd.MM · HH:mm}–{Session.Dto.Start.AddMinutes(Session.Dto.DurationMinutes):HH:mm}";

    public double Progress => Session is null || Session.Dto.Capacity == 0
        ? 0 : (Session.Dto.Capacity - Session.FreeSpots) / (double)Session.Dto.Capacity;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Session", out var dto)) _dto = dto as SessionDto;
        LoadCommand.Execute(null);
    }

    /// <summary>Uuenda, kui lehele tagasi tullakse (nt pärast kuukaardi ostu).</summary>
    public void Refresh()
    {
        if (Session is not null) LoadCommand.Execute(null);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        if (_dto is null) return;

        // Värsked andmed sama päeva tunniplaanist (vabad kohad võisid muutuda).
        var sameDay = await _api.GetSessionsAsync(_dto.Start.Date, _dto.Start.Date, null);
        _dto = sameDay.FirstOrDefault(s => s.Id == _dto.Id) ?? _dto;
        Session = new SessionItem(_dto);

        var pass = await _api.GetMyPassAsync();
        HasNoPass = pass is null;
        PassInfo = pass is null
            ? "Registreerumiseks on vaja kehtivat kuukaarti."
            : $"Registreerumine kasutab 1 külastuse. Kuukaardil on alles {pass.VisitsLeft} / {pass.VisitsTotal}.";

        var mine = (await _api.GetMyBookingsAsync()).FirstOrDefault(b => b.SessionId == _dto.Id);
        IsBooked = mine is not null;
        BookedLabel = mine?.Status == "Waitlisted"
            ? $"Oled ootejärjekorras, koht #{mine.Position}"
            : "Sul on sellele treeningule koht ✓";
    });

    [RelayCommand]
    private Task BookAsync() => RunAsync(async () =>
    {
        if (_dto is null) return;
        var result = await _api.BookAsync(_dto.Id);
        IsBooked = true;
        if (result.Waitlisted)
            await ToastAsync("Treening on täis", $"Sind lisati ootejärjekorda, sinu koht: {result.Position}. Kui koht vabaneb, saad automaatselt treeningule.");
        else
            await ToastAsync("Registreeritud!", $"{Session?.Name} – {When}. Meeldetuletus tuleb tund enne algust.");
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task BuyPassAsync() => Shell.Current.GoToAsync("buypass");

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    /// <summary>Täis treeningul paneb sama registreerumine kliendi automaatselt ootejärjekorda.</summary>
    [RelayCommand]
    private Task JoinWaitlistAsync() => BookAsync();
}

public partial class BookingsViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    public BookingsViewModel(ApiClient api) => _api = api;

    public ObservableCollection<BookingItem> Upcoming { get; } = new();
    public ObservableCollection<WaitlistItem> Waitlist { get; } = new();
    public ObservableCollection<BookingItem> History { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUpcoming), nameof(ShowWaitlist), nameof(ShowHistory),
        nameof(Tab0Background), nameof(Tab1Background), nameof(Tab2Background))]
    private int selectedTab;

    [ObservableProperty] private string waitlistTitle = "Ootel";

    public bool ShowUpcoming => SelectedTab == 0;
    public bool ShowWaitlist => SelectedTab == 1;
    public bool ShowHistory => SelectedTab == 2;
    public Color Tab0Background => SelectedTab == 0 ? Colors.White : Colors.Transparent;
    public Color Tab1Background => SelectedTab == 1 ? Colors.White : Colors.Transparent;
    public Color Tab2Background => SelectedTab == 2 ? Colors.White : Colors.Transparent;

    [RelayCommand]
    private void SelectTab(string index) => SelectedTab = int.Parse(index);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        // Üks päring: broneeringud (Booked) ja järjekorrakohad (Waitlisted) koos.
        var all = await _api.GetMyBookingsAsync();
        var now = DateTime.Now;

        Upcoming.Clear();
        foreach (var b in all.Where(b => b.Status == "Booked" && b.Start > now)) Upcoming.Add(new BookingItem(b));
        History.Clear();
        foreach (var b in all.Where(b => b.Status == "Booked" && b.Start <= now).OrderByDescending(b => b.Start)) History.Add(new BookingItem(b));
        Waitlist.Clear();
        foreach (var w in all.Where(b => b.Status == "Waitlisted")) Waitlist.Add(new WaitlistItem(w));
        WaitlistTitle = Waitlist.Count > 0 ? $"Ootel ({Waitlist.Count})" : "Ootel";
    });

    [RelayCommand]
    private async Task CancelAsync(BookingItem item)
    {
        if (!await Shell.Current.DisplayAlertAsync("Tühista broneering", $"{item.Name}, {item.When}?\nKülastus läheb kuukaardile tagasi.", "Tühista", "Ei")) return;
        await RunAsync(() => _api.CancelBookingAsync(item.Dto.Id));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LeaveAsync(WaitlistItem item)
    {
        if (!await Shell.Current.DisplayAlertAsync("Lahku järjekorrast", $"{item.Name}, {item.When}?", "Lahku", "Ei")) return;
        // Järjekorrast lahkumine = sama tühistamine (DELETE /api/bookings/{id}).
        await RunAsync(() => _api.CancelBookingAsync(item.Dto.Id));
        await LoadAsync();
    }
}

public partial class PassViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public PassViewModel(ApiClient api, AuthState auth, RealtimeService realtime)
    {
        _api = api;
        _auth = auth;
        _realtime = realtime;
    }

    [ObservableProperty] private MonthlyPassDto? pass;
    [ObservableProperty] private bool hasPass;
    [ObservableProperty] private string monthTitle = "";
    [ObservableProperty] private string validText = "";
    [ObservableProperty] private double usedProgress;
    [ObservableProperty] private string upcomingText = "";
    [ObservableProperty] private bool hasUpcoming;
    [ObservableProperty] private string buyButtonText = "Osta kuukaart";

    public ObservableCollection<BookingItem> Visits { get; } = new();

    public string UserName => _auth.User?.Name ?? "";
    public string UserEmail => _auth.User?.Email ?? "";
    public string Initials => string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(Initials));

        // Korraga on ainult üks aktiivne kuukaart (404 → null).
        Pass = await _api.GetMyPassAsync();
        HasPass = Pass is not null;
        HasUpcoming = false;
        UpcomingText = "";
        BuyButtonText = "Osta kuukaart";
        if (Pass is not null)
        {
            MonthTitle = Pass.Plan;
            ValidText = $"Kehtib {Pass.From:dd.MM.yyyy} – {Pass.Until:dd.MM.yyyy}";
            UsedProgress = Pass.VisitsTotal == 0 ? 0 : Pass.VisitsLeft / (double)Pass.VisitsTotal;
        }

        var bookings = await _api.GetMyBookingsAsync();
        Visits.Clear();
        foreach (var b in bookings.Where(b => b.Status == "Booked" && b.Start <= DateTime.Now).OrderByDescending(b => b.Start).Take(5))
            Visits.Add(new BookingItem(b));
    });

    [RelayCommand]
    private Task BuyPassAsync() => Shell.Current.GoToAsync("buypass");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _realtime.Stop();
        _auth.SignOut();
        await Shell.Current.GoToAsync("//login");
    }
}
