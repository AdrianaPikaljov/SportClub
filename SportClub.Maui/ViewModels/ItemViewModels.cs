using CommunityToolkit.Mvvm.ComponentModel;
using SportClub.Maui.Models;

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

    public SessionDto Dto { get; }
    public int Id => Dto.Id;
    public string Name => Dto.Type;
    public string Trainer => Dto.Trainer;
    public string Time => Dto.Start.ToString("HH:mm");
    public string Day => Dto.Start.ToString("ddd, dd.MM");
    public string Duration => $"{Dto.DurationMinutes} min";
    public string Subtitle => $"{Dto.Trainer} · {Dto.DurationMinutes} min";
    public string CapacityText => $"{Dto.Capacity} kohta";
    /// <summary>Treeneri vaates: "Tulemas", "Kohalolu märkimata" või "Kohalolu märgitud".</summary>
    public string StatusText { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge), nameof(BadgeBackground), nameof(BadgeTextColor), nameof(IsFull), nameof(BookedText))]
    private int freeSpots;

    public bool IsFull => FreeSpots == 0;
    public string BookedText => $"{Dto.Capacity - FreeSpots}/{Dto.Capacity}";

    public string Badge => FreeSpots switch
    {
        0 => "Täis",
        <= 3 => $"{FreeSpots} vaba",
        _ => $"{FreeSpots}/{Dto.Capacity} vaba"
    };

    public Color BadgeBackground => FreeSpots switch
    {
        0 => Color.FromArgb("#FDE8DC"),
        <= 3 => Color.FromArgb("#FFF1D6"),
        _ => Color.FromArgb("#E1F4E7")
    };

    public Color BadgeTextColor => FreeSpots switch
    {
        0 => Color.FromArgb("#A33A08"),
        <= 3 => Color.FromArgb("#8A4B00"),
        _ => Color.FromArgb("#17663A")
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

    public Color Background => IsSelected ? Color.FromArgb("#14161A") : Colors.White;
    public Color TextColor => IsSelected ? Color.FromArgb("#F4F5F2") : Color.FromArgb("#14161A");
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
    [NotifyPropertyChangedFor(nameof(Background))]
    private bool isSelected;

    public Color Background => IsSelected ? Color.FromArgb("#D4FF3A") : Colors.White;
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
    public string Tag => Dto.Attended switch { true => "Kohal", false => "Puudus", _ => Dto.Start > DateTime.Now ? "Kinnitatud" : "Märkimata" };
}

/// <summary>Koht ootejärjekorras (sama BookingDto, status = Waitlisted).</summary>
public class WaitlistItem
{
    public WaitlistItem(BookingDto dto) => Dto = dto;

    public BookingDto Dto { get; }
    public string When => Dto.Start.ToString("ddd, dd.MM · HH:mm");
    public string Name => Dto.Type;
    public string Tag => $"Järjekorras #{Dto.Position}";
}

public partial class ParticipantItem : ObservableObject
{
    public ParticipantItem(AttendanceItemDto dto)
    {
        Dto = dto;
        attended = dto.Attended;
    }

    public AttendanceItemDto Dto { get; }
    public string Name => Dto.Name ?? "";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PresentBackground), nameof(AbsentBackground), nameof(PresentTextColor), nameof(AbsentTextColor))]
    private bool? attended;

    public Color PresentBackground => Attended == true ? Color.FromArgb("#1F8A4C") : Colors.White;
    public Color PresentTextColor => Attended == true ? Colors.White : Color.FromArgb("#5B616B");
    public Color AbsentBackground => Attended == false ? Color.FromArgb("#C2410C") : Colors.White;
    public Color AbsentTextColor => Attended == false ? Colors.White : Color.FromArgb("#5B616B");
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
    public string Detail => $"{Dto.Bookings} broneeringut · kohal {Dto.Attended} · täituvus {Dto.FillRate}%";
    public double Progress { get; }
    public Color BarColor => Rank == 1 ? Color.FromArgb("#14161A") : Color.FromArgb("#7A808A");
}
