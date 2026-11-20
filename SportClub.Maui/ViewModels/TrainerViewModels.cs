using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

public partial class TrainerSessionsViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RealtimeService _realtime;

    public TrainerSessionsViewModel(ApiClient api, AuthState auth, RealtimeService realtime)
    {
        _api = api;
        _auth = auth;
        _realtime = realtime;
        _realtime.FreeSpotsChanged += (_, m) =>
        {
            foreach (var s in Sessions.Where(s => s.Id == m.SessionId)) s.FreeSpots = m.Free;
            if (Next?.Id == m.SessionId) Next.FreeSpots = m.Free;
        };
    }

    public ObservableCollection<SessionItem> Sessions { get; } = new();

    [ObservableProperty] private SessionItem? next;
    [ObservableProperty] private bool hasNext;
    [ObservableProperty] private string nextLabel = "";

    public string Greeting => $"Tere, {_auth.User?.FullName.Split(' ')[0]}";

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        OnPropertyChanged(nameof(Greeting));
        var page = await _api.GetMyTrainerSessionsAsync();
        var items = page.Items.Select(s => new SessionItem(s)).ToList();

        Next = items.FirstOrDefault(s => s.Dto.StartsAt.AddMinutes(s.Dto.DurationMinutes) > DateTime.UtcNow);
        HasNext = Next is not null;
        if (Next is not null)
        {
            var minutes = (int)(Next.Dto.StartsAt - DateTime.UtcNow).TotalMinutes;
            NextLabel = minutes <= 0 ? "Käimas" : minutes < 120 ? $"Järgmine · {minutes} min pärast" : $"Järgmine · {Next.Day}";
        }

        Sessions.Clear();
        foreach (var s in items.Where(s => s != Next)) Sessions.Add(s);
    });

    [RelayCommand]
    private Task OpenAsync(SessionItem item) =>
        Shell.Current.GoToAsync("attendance", new Dictionary<string, object> { ["Session"] = item });

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _realtime.Stop();
        _auth.SignOut();
        await Shell.Current.GoToAsync("//login");
    }
}

public partial class AttendanceViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;

    public AttendanceViewModel(ApiClient api) => _api = api;

    public ObservableCollection<ParticipantItem> Participants { get; } = new();

    [ObservableProperty] private SessionItem? session;
    [ObservableProperty] private int present;
    [ObservableProperty] private int absent;
    [ObservableProperty] private int unmarked;
    [ObservableProperty] private bool isEmpty;

    public string Title => Session is null ? "" : $"{Session.Name} · {Session.Time}";
    public string SubTitle => Session is null ? "" : $"{Session.Day} · {Session.Dto.Room}";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Session = query["Session"] as SessionItem;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubTitle));
        LoadCommand.Execute(null);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        if (Session is null) return;
        var list = await _api.GetParticipantsAsync(Session.Id);
        Participants.Clear();
        foreach (var p in list) Participants.Add(new ParticipantItem(p));
        IsEmpty = Participants.Count == 0;
        Recount();
    });

    [RelayCommand]
    private Task MarkPresentAsync(ParticipantItem item) => MarkAsync(item, true);

    [RelayCommand]
    private Task MarkAbsentAsync(ParticipantItem item) => MarkAsync(item, false);

    private Task MarkAsync(ParticipantItem item, bool attended) => RunAsync(async () =>
    {
        await _api.SetAttendanceAsync(item.Dto.BookingId, attended);
        item.Attended = attended;
        Recount();
    });

    private void Recount()
    {
        Present = Participants.Count(p => p.Attended == true);
        Absent = Participants.Count(p => p.Attended == false);
        Unmarked = Participants.Count(p => p.Attended is null);
    }
}

public partial class StatsViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    public StatsViewModel(ApiClient api) => _api = api;

    public ObservableCollection<StatItem> Rows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tab0Background), nameof(Tab1Background), nameof(Tab2Background))]
    private int period = 1;

    [ObservableProperty] private int totalBookings;
    [ObservableProperty] private string averageFill = "0%";

    public Color Tab0Background => Period == 0 ? Colors.White : Colors.Transparent;
    public Color Tab1Background => Period == 1 ? Colors.White : Colors.Transparent;
    public Color Tab2Background => Period == 2 ? Colors.White : Colors.Transparent;

    [RelayCommand]
    private async Task SelectPeriodAsync(string index)
    {
        Period = int.Parse(index);
        await LoadAsync();
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        DateTime? from = Period switch
        {
            0 => DateTime.UtcNow.AddDays(-7),
            1 => DateTime.UtcNow.AddMonths(-1),
            _ => null
        };
        var rows = await _api.GetPopularAsync(from);
        var max = rows.Count == 0 ? 0 : rows.Max(r => r.Bookings);
        Rows.Clear();
        for (var i = 0; i < rows.Count; i++) Rows.Add(new StatItem(i + 1, rows[i], max));
        TotalBookings = rows.Sum(r => r.Bookings);
        AverageFill = rows.Count == 0 ? "0%" : $"{rows.Average(r => r.AverageFillPercent):0.#}%";
    });
}
