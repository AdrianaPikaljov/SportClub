using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SportClub.Maui.Models;
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
            foreach (var s in Groups.SelectMany(g => g).Where(s => s.Id == m.SessionId)) s.FreeSpots = m.Free;
            if (Next?.Id == m.SessionId) Next.FreeSpots = m.Free;
        };
    }

    /// <summary>
    /// Grupid: märkimata kohaloluga (esile tõstetud), tulevased, toimunud.
    /// Iga laadimisega antakse uus nimekiri (mitte Clear/Add) – grupeeritud CollectionView kukkus Windowsis muidu kokku.
    /// </summary>
    [ObservableProperty] private List<SessionGroup> groups = new();

    [ObservableProperty] private SessionItem? next;
    [ObservableProperty] private bool hasNext;
    [ObservableProperty] private string nextLabel = "";
    [ObservableProperty] private bool hasUnmarked;
    [ObservableProperty] private string unmarkedText = "";

    public string Greeting => Loc.T("trainer.greeting", _auth.User?.Name.Split(' ')[0]);

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        OnPropertyChanged(nameof(Greeting));
        // API annab kõik treeneri treeningud (uuemad eespool) koos staatusega.
        var list = await _api.GetMyTrainerSessionsAsync();
        var trainerName = _auth.User?.Name ?? "";
        var items = list.Select(ToItem).ToList();

        // Järgmine = varaseim treening, mis pole veel lõppenud.
        Next = items.Where(s => s.Dto.Start.AddMinutes(s.Dto.DurationMinutes) > DateTime.Now)
                    .OrderBy(s => s.Dto.Start).FirstOrDefault();
        HasNext = Next is not null;
        if (Next is not null)
        {
            var minutes = (int)(Next.Dto.Start - DateTime.Now).TotalMinutes;
            NextLabel = minutes <= 0 ? Loc.T("trainer.now") : minutes < 120 ? Loc.T("trainer.inMinutes", minutes) : Loc.T("trainer.nextDay", Next.Day);
        }

        // Toimunud treeningud, kus keegi on märkimata, on alati üleval ja punased – nii ei jää ükski märkimata.
        var now = DateTime.Now;
        var unmarked = items.Where(s => s.NeedsAttention && s != Next).OrderBy(s => s.Dto.Start).ToList();
        var upcoming = items.Where(s => s.Dto.Start > now && s != Next).OrderBy(s => s.Dto.Start).ToList();
        var past = items.Where(s => s.Dto.Start <= now && !s.NeedsAttention && s != Next).OrderByDescending(s => s.Dto.Start).ToList();

        HasUnmarked = unmarked.Count > 0;
        UnmarkedText = Loc.T("trainer.unmarkedWarning", unmarked.Count, unmarked.Sum(s => s.Unmarked));

        var groups = new List<SessionGroup>();
        if (unmarked.Count > 0) groups.Add(new SessionGroup(Loc.T("trainer.groupUnmarked", unmarked.Count), true, unmarked));
        if (upcoming.Count > 0) groups.Add(new SessionGroup(Loc.T("trainer.groupUpcoming"), false, upcoming));
        if (past.Count > 0) groups.Add(new SessionGroup(Loc.T("trainer.groupPast"), false, past));
        Groups = groups;
    });

    private SessionItem ToItem(TrainerSessionDto t)
    {
        var needsAttention = t.Status == "Kohalolu märkimata" && t.Unmarked > 0;
        var status = needsAttention
            ? Loc.T("trainer.unmarkedCount", t.Unmarked, t.Booked)
            : Loc.TrainerStatus(t.Status);
        if (t.Waitlisted > 0) status = Loc.T("trainer.waiting", status, t.Waitlisted);

        return new SessionItem(
            new SessionDto(t.Id, t.Type, 0, _auth.User?.Name ?? "", t.StartsAt, t.DurationMinutes, t.Capacity, t.Capacity - t.Booked), status)
        {
            NeedsAttention = needsAttention,
            Unmarked = t.Unmarked
        };
    }

    [RelayCommand]
    private Task OpenAsync(SessionItem item) =>
        Shell.Current.GoToAsync("attendance", new Dictionary<string, object> { ["Session"] = item });

    public override void OnPreferencesChanged() => LoadCommand.Execute(null);

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
    [ObservableProperty] private bool hasUnmarked;
    [ObservableProperty] private bool isEmpty;

    public string Title => Session is null ? "" : $"{Session.Name} · {Session.Time}";
    public string SubTitle => Session is null ? "" : Loc.T("att.subtitle", Session.Day, Session.Duration, Session.BookedText);

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
        var list = await _api.GetAttendanceAsync(Session.Id);
        Participants.Clear();
        foreach (var p in list) Participants.Add(new ParticipantItem(p));
        IsEmpty = Participants.Count == 0;
        Recount();
    });

    [RelayCommand]
    private Task BackAsync() => GoBackAsync();

    public override void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(SubTitle));
        LoadCommand.Execute(null);
    }

    /// <summary>Märgib kõik veel märkimata osalejad kohal olnuks (üks päring).</summary>
    [RelayCommand]
    private Task MarkRestPresentAsync() => RunAsync(async () =>
    {
        if (Session is null) return;
        var rest = Participants.Where(p => p.Attended is null).ToList();
        if (rest.Count == 0) return;
        await _api.SaveAttendanceAsync(Session.Id, rest.Select(p => p.Dto with { Attended = true }));
        foreach (var p in rest) p.Attended = true;
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
        HasUnmarked = Unmarked > 0;
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

    public Color Tab0Background => Period == 0 ? ThemeService.Get("Surface") : Colors.Transparent;
    public Color Tab1Background => Period == 1 ? ThemeService.Get("Surface") : Colors.Transparent;
    public Color Tab2Background => Period == 2 ? ThemeService.Get("Surface") : Colors.Transparent;

    public override void OnPreferencesChanged()
    {
        OnPropertyChanged(nameof(Tab0Background));
        OnPropertyChanged(nameof(Tab1Background));
        OnPropertyChanged(nameof(Tab2Background));
        LoadCommand.Execute(null);
    }

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
            0 => DateTime.Today.AddDays(-7),
            1 => DateTime.Today.AddMonths(-1),
            _ => null
        };
        OnPropertyChanged(nameof(Tab0Background));
        OnPropertyChanged(nameof(Tab1Background));
        OnPropertyChanged(nameof(Tab2Background));
        var rows = await _api.GetPopularAsync(from);
        var max = rows.Count == 0 ? 0 : rows.Max(r => r.Bookings);
        Rows.Clear();
        for (var i = 0; i < rows.Count; i++) Rows.Add(new StatItem(i + 1, rows[i], max));
        TotalBookings = rows.Sum(r => r.Bookings);
        AverageFill = rows.Count == 0 ? "0%" : $"{rows.Average(r => r.FillRate):0.#}%";
    });
}
