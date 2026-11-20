using CommunityToolkit.Mvvm.ComponentModel;
using SportClub.Maui.Models;

namespace SportClub.Maui.ViewModels;

/// <summary>One session card. FreeSpots changes live from the WebSocket.</summary>
public partial class SessionItem : ObservableObject
{
    public SessionItem(SessionDto dto)
    {
        Dto = dto;
        freeSpots = dto.FreeSpots;
    }

    public SessionDto Dto { get; }
    public int Id => Dto.Id;
    public string Name => Dto.TrainingTypeName;
    public string Time => Dto.StartsAt.ToLocalTime().ToString("HH:mm");
    public string Day => Dto.StartsAt.ToLocalTime().ToString("ddd, dd.MM");
    public string Duration => $"{Dto.DurationMinutes} min";
    public string Subtitle => $"{Dto.TrainerName} · {Dto.Room}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Badge), nameof(BadgeBackground), nameof(BadgeTextColor), nameof(IsFull), nameof(BookedText))]
    private int freeSpots;

    public bool IsFull => FreeSpots == 0;
    public string BookedText => $"{Dto.MaxParticipants - FreeSpots}/{Dto.MaxParticipants}";

    public string Badge => FreeSpots switch
    {
        0 => "Täis",
        <= 3 => $"{FreeSpots} vaba",
        _ => $"{FreeSpots}/{Dto.MaxParticipants} vaba"
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
    public ChipItem(int? id, string label)
    {
        Id = id;
        Label = label;
    }

    public int? Id { get; }
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
    public string When => Dto.StartsAt.ToLocalTime().ToString("ddd, dd.MM · HH:mm");
    public string Name => Dto.TrainingTypeName;
    public string Subtitle => $"{Dto.TrainerName} · {Dto.Room}";
    public bool IsCancelled => Dto.Status == "Cancelled";
    public bool CanCancel => !IsCancelled && Dto.StartsAt > DateTime.UtcNow;
    public string Tag => IsCancelled ? "Tühistatud" : Dto.Attended switch { true => "Kohal", false => "Puudus", _ => "Kinnitatud" };
}

public class WaitlistItem
{
    public WaitlistItem(WaitlistEntryDto dto) => Dto = dto;

    public WaitlistEntryDto Dto { get; }
    public string When => Dto.StartsAt.ToLocalTime().ToString("ddd, dd.MM · HH:mm");
    public string Name => Dto.TrainingTypeName;
    public string Tag => $"Järjekorras #{Dto.Position}";
}

public partial class ParticipantItem : ObservableObject
{
    public ParticipantItem(ParticipantDto dto)
    {
        Dto = dto;
        attended = dto.Attended;
    }

    public ParticipantDto Dto { get; }
    public string Name => Dto.FullName;
    public string Initials => string.Concat(Dto.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0])).ToUpperInvariant();

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
    public StatItem(int rank, PopularTrainingDto dto, int maxBookings)
    {
        Rank = rank;
        Dto = dto;
        Progress = maxBookings == 0 ? 0 : dto.Bookings / (double)maxBookings;
    }

    public int Rank { get; }
    public PopularTrainingDto Dto { get; }
    public string Title => $"{Rank}. {Dto.Name}";
    public string Detail => $"{Dto.Bookings} broneeringut · täituvus {Dto.AverageFillPercent}%";
    public double Progress { get; }
    public Color BarColor => Rank == 1 ? Color.FromArgb("#14161A") : Color.FromArgb("#7A808A");
}
