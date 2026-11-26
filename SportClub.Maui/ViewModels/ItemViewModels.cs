using CommunityToolkit.Mvvm.ComponentModel;
using SportClub.Maui.Models;
using SportClub.Maui.Services;

namespace SportClub.Maui.ViewModels;

/// <summary>Üks treeningu kaart. FreeSpots uueneb WebSocketi kaudu reaalajas.</summary>
public partial class SessionItem : ObservableObject
{
    public SessionItem(SessionDto dto, string status = "")
    {
        Dto = dto;
        freeSpots = dto.Free;
        StatusText = status;
    }

    /// <summary>"5/12 broneeritud" / "5/12 booked"</summary>
    public string BookedLabel => Loc.T("detail.booked", BookedText);

    public SessionDto Dto { get; }
    public int Id => Dto.Id;
    public string Name => Dto.Type;
    public string Trainer => Dto.Trainer;
    public string Time => Dto.Start.ToString("HH:mm");
    public string Day => Dto.Start.ToString("ddd, dd.MM");
    public string Duration => Loc.T("minutes", Dto.DurationMinutes);
    public string Subtitle => $"{Dto.Trainer} · {Dto.DurationMinutes} min";
    public string CapacityText => Loc.T("detail.capacity", Dto.Capacity);
    /// <summary>Treeneri vaates: "Tulemas", "Kohalolu märkimata" või "Kohalolu märgitud".</summary>
    public string StatusText { get; }

    /// <summary>Treeneri vaates: toimunud treening, kus kõigil pole kohalolu märgitud – kaart on punaselt esile tõstetud.</summary>
    public bool NeedsAttention { get; init; }
    public int Unmarked { get; init; }
    public Color CardBackground => NeedsAttention ? ThemeService.Get("FullBg") : ThemeService.Get("Surface");
    public Color CardStroke => NeedsAttention ? ThemeService.Get("FullText") : ThemeService.Get("Line");
    public Color StatusColor => NeedsAttention ? ThemeService.Get("FullText") : ThemeService.Get("Muted");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge), nameof(BadgeBackground), nameof(BadgeTextColor), nameof(IsFull), nameof(BookedText), nameof(BookedLabel))]
    private int freeSpots;

    public bool IsFull => FreeSpots == 0;
    public string BookedText => $"{Dto.Capacity - FreeSpots}/{Dto.Capacity}";

    public string Badge => FreeSpots switch
    {
        0 => Loc.T("badge.full"),
        <= 3 => Loc.T("badge.free", FreeSpots),
        _ => Loc.T("badge.freeOf", FreeSpots, Dto.Capacity)
    };

    public Color BadgeBackground => FreeSpots switch
    {
        0 => ThemeService.Get("FullBg"),
        <= 3 => ThemeService.Get("FewBg"),
        _ => ThemeService.Get("OkBg")
    };

    public Color BadgeTextColor => FreeSpots switch
    {
        0 => ThemeService.Get("FullText"),
        <= 3 => ThemeService.Get("FewText"),
        _ => ThemeService.Get("OkText")
    };
}

public partial class DayItem : ObservableObject
{
    public DayItem(DateTime date) => Date = date;

    public DateTime Date { get; }
    public string WeekDay => Date.ToString("ddd").TrimEnd('.').ToUpperInvariant()[..1];
    public string Number => Date.Day.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(TextColor))]
    private bool isSelected;

    public Color Background => IsSelected ? ThemeService.Get("Selected") : ThemeService.Get("Surface");
    public Color TextColor => IsSelected ? ThemeService.Get("OnSelected") : ThemeService.Get("Ink");

    /// <summary>Pärast teema vahetust: värvid uuesti.</summary>
    public void RefreshColors() => OnPropertyChanged(string.Empty);
}

public partial class ChipItem : ObservableObject
{
    /// <param name="type">Tüübi nimi API jaoks (null = kõik).</param>
    public ChipItem(string? type, string label)
    {
        Type = type;
        Label = label;
    }

    public string? Type { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background), nameof(TextColor))]
    private bool isSelected;

    public Color Background => IsSelected ? Color.FromArgb("#D4FF3A") : ThemeService.Get("Surface");
    public Color TextColor => IsSelected ? Color.FromArgb("#14161A") : ThemeService.Get("Ink");

    public void RefreshColors() => OnPropertyChanged(string.Empty);
}

public class BookingItem
{
    public BookingItem(BookingDto dto) => Dto = dto;

    public BookingDto Dto { get; }
    public string When => Dto.Start.ToString("ddd, dd.MM · HH:mm");
    public string Name => Dto.Type;
    public string Subtitle => Dto.Trainer;
    /// <summary>Tühistada saab kuni 2 tundi enne algust (API kontrollib sama reeglit).</summary>
    public bool CanCancel => Dto.Start > DateTime.Now.AddHours(2);
    public string Tag => Dto.Attended switch
    {
        true => Loc.T("tag.present"),
        false => Loc.T("tag.absent"),
        _ => Dto.Start > DateTime.Now ? Loc.T("tag.confirmed") : Loc.T("tag.unmarked")
    };
}

/// <summary>Koht ootejärjekorras (sama BookingDto, status = Waitlisted).</summary>
public class WaitlistItem
{
    public WaitlistItem(BookingDto dto) => Dto = dto;

    public BookingDto Dto { get; }
    public string When => Dto.Start.ToString("ddd, dd.MM · HH:mm");
    public string Name => Dto.Type;
    public string Tag => Loc.T("tag.queue", Dto.Position);
}

public partial class ParticipantItem : ObservableObject
{
    /// <summary>Märkimata osaleja rida on esile tõstetud.</summary>
    public Color RowBackground => Attended is null ? ThemeService.Get("FullBg") : ThemeService.Get("Surface");
    public Color RowStroke => Attended is null ? ThemeService.Get("FullText") : ThemeService.Get("Line");

    public ParticipantItem(AttendanceItemDto dto)
    {
        Dto = dto;
        attended = dto.Attended;
    }

    public AttendanceItemDto Dto { get; }
    public string Name => Dto.Name ?? "";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PresentBackground), nameof(AbsentBackground), nameof(PresentTextColor), nameof(AbsentTextColor), nameof(RowBackground), nameof(RowStroke))]
    private bool? attended;

    public Color PresentBackground => Attended == true ? Color.FromArgb("#1F8A4C") : ThemeService.Get("Surface");
    public Color PresentTextColor => Attended == true ? Colors.White : ThemeService.Get("Muted");
    public Color AbsentBackground => Attended == false ? Color.FromArgb("#C2410C") : ThemeService.Get("Surface");
    public Color AbsentTextColor => Attended == false ? Colors.White : ThemeService.Get("Muted");
}

/// <summary>Treeneri nimekirja grupp (Märkimata / Tulevased / Toimunud).</summary>
public class SessionGroup : List<SessionItem>
{
    public SessionGroup(string title, bool warning, IEnumerable<SessionItem> items) : base(items)
    {
        Title = title;
        IsWarning = warning;
    }

    public string Title { get; }
    public bool IsWarning { get; }
    public Color TitleColor => IsWarning ? ThemeService.Get("FullText") : ThemeService.Get("Muted");
}

public class StatItem
{
    public StatItem(int rank, PopularDto dto, int maxBookings)
    {
        Rank = rank;
        Dto = dto;
        Progress = maxBookings == 0 ? 0 : dto.Bookings / (double)maxBookings;
    }

    public int Rank { get; }
    public PopularDto Dto { get; }
    public string Title => $"{Rank}. {Dto.Type}";
    public string Detail => Loc.T("stats.row", Dto.Bookings, Dto.Attended, Dto.FillRate);
    public double Progress { get; }
    public Color BarColor => Rank == 1 ? ThemeService.Get("TopBar") : ThemeService.Get("Bar");
}
