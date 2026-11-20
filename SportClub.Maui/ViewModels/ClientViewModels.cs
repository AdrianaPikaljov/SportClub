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
        if (response.User.Role is not ("Client" or "Trainer"))
        {
            ErrorMessage = "Mobiilirakendus on klientidele ja treeneritele. Admin kasutab veebiliidest.";
            return;
        }
        await _auth.SignInAsync(response);
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
            foreach (var t in types.Items) Types.Add(new ChipItem(t.Id, t.Name));
            _typesLoaded = true;
        }

        var day = Days.First(d => d.IsSelected).Date;
        var from = day == DateTime.Today ? DateTime.Now : day; // today: only sessions that have not started
        var typeId = Types.First(t => t.IsSelected).Id;
        var page = await _api.GetSessionsAsync(from.ToUniversalTime(), day.AddDays(1).ToUniversalTime(), typeId);

        Sessions.Clear();
        foreach (var s in page.Items) Sessions.Add(new SessionItem(s));
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
        Shell.Current.GoToAsync("session", new Dictionary<string, object> { ["Id"] = item.Id });
}

public partial class SessionDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private int _id;

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
    [ObservableProperty] private bool hasNoPass;

    public string When => Session is null ? "" :
        $"{Session.Dto.StartsAt.ToLocalTime():ddd, dd.MM · HH:mm}–{Session.Dto.StartsAt.AddMinutes(Session.Dto.DurationMinutes).ToLocalTime():HH:mm}";

    public double Progress => Session is null || Session.Dto.MaxParticipants == 0
        ? 0 : (Session.Dto.MaxParticipants - Session.FreeSpots) / (double)Session.Dto.MaxParticipants;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Id", out var id)) _id = Convert.ToInt32(id);
        LoadCommand.Execute(null);
    }

    /// <summary>Reload when coming back (e.g. after buying a pass).</summary>
    public void Refresh()
    {
        if (Session is not null) LoadCommand.Execute(null);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var dto = await _api.GetSessionAsync(_id);
        Session = new SessionItem(dto);

        var passes = await _api.GetMyPassesAsync();
        var active = passes.FirstOrDefault(p => p.IsActive);
        HasNoPass = active is null;
        PassInfo = active is null
            ? "Broneerimiseks on vaja kehtivat kuukaarti."
            : $"Broneering kasutab 1 külastuse. Kuukaardil on alles {active.VisitsLeft} / {active.VisitsTotal}.";

        var bookings = await _api.GetMyBookingsAsync(upcoming: true);
        IsBooked = bookings.Any(b => b.SessionId == _id);
    });

    [RelayCommand]
    private Task BookAsync() => RunAsync(async () =>
    {
        await _api.BookAsync(_id);
        IsBooked = true;
        await ToastAsync("Broneeritud!", $"{Session?.Name} – {When}. Meeldetuletus tuleb tund enne algust.");
        await Shell.Current.GoToAsync("..");
    });

    [RelayCommand]
    private Task BuyPassAsync() => Shell.Current.GoToAsync("buypass");

    [RelayCommand]
    private Task JoinWaitlistAsync() => RunAsync(async () =>
    {
        var entry = await _api.JoinWaitlistAsync(_id);
        await ToastAsync("Oled ootejärjekorras", $"Sinu koht järjekorras: {entry.Position}. Kui koht vabaneb, saad automaatselt treeningule.");
        await Shell.Current.GoToAsync("..");
    });
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
        var upcoming = await _api.GetMyBookingsAsync(upcoming: true);
        var all = await _api.GetMyBookingsAsync(upcoming: false);
        var waitlist = await _api.GetMyWaitlistAsync();

        Upcoming.Clear();
        foreach (var b in upcoming) Upcoming.Add(new BookingItem(b));
        History.Clear();
        foreach (var b in all.Where(b => b.StartsAt <= DateTime.UtcNow || b.Status == "Cancelled")) History.Add(new BookingItem(b));
        Waitlist.Clear();
        foreach (var w in waitlist) Waitlist.Add(new WaitlistItem(w));
        WaitlistTitle = Waitlist.Count > 0 ? $"Ootel ({Waitlist.Count})" : "Ootel";
    });

    [RelayCommand]
    private async Task CancelAsync(BookingItem item)
    {
        if (!await Shell.Current.DisplayAlertAsync("Tühista broneering", $"{item.Name}, {item.When}?", "Tühista", "Ei")) return;
        await RunAsync(() => _api.CancelBookingAsync(item.Dto.Id));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LeaveAsync(WaitlistItem item)
    {
        if (!await Shell.Current.DisplayAlertAsync("Lahku järjekorrast", $"{item.Name}, {item.When}?", "Lahku", "Ei")) return;
        await RunAsync(() => _api.LeaveWaitlistAsync(item.Dto.Id));
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
    public ObservableCollection<NotificationDto> Notifications { get; } = new();

    public string UserName => _auth.User?.FullName ?? "";
    public string UserEmail => _auth.User?.Email ?? "";
    public string Initials => string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        OnPropertyChanged(nameof(UserName));
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(Initials));

        var passes = await _api.GetMyPassesAsync();
        Pass = passes.Where(p => p.IsActive).OrderBy(p => p.ValidUntil).FirstOrDefault()
               ?? passes.Where(p => !p.IsUpcoming).FirstOrDefault();
        HasPass = Pass is not null;
        var upcoming = passes.Where(p => p.IsUpcoming).OrderBy(p => p.ValidFrom).FirstOrDefault();
        HasUpcoming = upcoming is not null;
        UpcomingText = upcoming is null ? "" :
            $"Järgmine kuukaart: {upcoming.PlanName ?? "kuukaart"} ({upcoming.VisitsTotal} külastust) kehtib alates {upcoming.ValidFrom.ToLocalTime():dd.MM.yyyy}.";
        BuyButtonText = Pass?.IsActive == true ? "Osta järgmine kuukaart" : "Osta kuukaart";
        if (Pass is not null)
        {
            MonthTitle = Pass.ValidFrom.ToLocalTime().ToString("MMMM yyyy");
            ValidText = $"Kehtib kuni {Pass.ValidUntil.ToLocalTime():dd.MM.yyyy}";
            UsedProgress = Pass.VisitsTotal == 0 ? 0 : Pass.VisitsLeft / (double)Pass.VisitsTotal;
        }

        var history = await _api.GetMyBookingsAsync(upcoming: false);
        Visits.Clear();
        foreach (var b in history.Where(b => b.Status == "Active" && b.StartsAt <= DateTime.UtcNow).Take(5))
            Visits.Add(new BookingItem(b));

        var notifications = await _api.GetMyNotificationsAsync();
        Notifications.Clear();
        foreach (var n in notifications.Take(5)) Notifications.Add(n);
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
