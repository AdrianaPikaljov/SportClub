namespace SportClub.Core.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Trainer = "Trainer";
    public const string Client = "Client";
    public const string Service = "Service";

    public static readonly string[] All = { Admin, Trainer, Client, Service };
}

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Client;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Trainer? Trainer { get; set; }
    public List<Booking> Bookings { get; set; } = new();
    public List<MonthlyPass> Passes { get; set; } = new();
}

public class Trainer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Bio { get; set; }
    public string? Phone { get; set; }

    /// <summary>Linked login account (role Trainer). Optional.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    public List<Session> Sessions { get; set; } = new();
}

public class TrainingType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Intensity { get; set; } = "Keskmine";

    public List<Session> Sessions { get; set; } = new();
}

/// <summary>One scheduled group training (a row in the timetable).</summary>
public class Session
{
    public int Id { get; set; }
    public int TrainingTypeId { get; set; }
    public TrainingType? TrainingType { get; set; }
    public int TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    /// <summary>Start time in UTC.</summary>
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    public string Room { get; set; } = "";
    public int MaxParticipants { get; set; }

    public List<Booking> Bookings { get; set; } = new();
    public List<WaitlistEntry> WaitlistEntries { get; set; } = new();
}

public enum BookingStatus
{
    Active = 0,
    Cancelled = 1
}

public class Booking
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public Session? Session { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? MonthlyPassId { get; set; }
    public MonthlyPass? MonthlyPass { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    /// <summary>null = not marked yet, true = present, false = absent.</summary>
    public bool? Attended { get; set; }
    public DateTime? ReminderSentAt { get; set; }
}

public class WaitlistEntry
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public Session? Session { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Queue order is FIFO by this timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A pass package the club sells (e.g. 12 visits for 30 days).</summary>
public class PassPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int Visits { get; set; }
    public int DurationDays { get; set; } = 30;
    public decimal Price { get; set; }
    /// <summary>Inactive plans are not sold any more, but old passes keep the link.</summary>
    public bool IsActive { get; set; } = true;
}

public class MonthlyPass
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Plan the pass was bought from; null if an admin created it manually.</summary>
    public int? PassPlanId { get; set; }
    public PassPlan? PassPlan { get; set; }
    public decimal? PricePaid { get; set; }
    public DateTime? PurchasedAt { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int VisitsTotal { get; set; }
    public int VisitsUsed { get; set; }

    public int VisitsLeft => VisitsTotal - VisitsUsed;

    public bool IsValidAt(DateTime utc) => utc >= ValidFrom && utc <= ValidUntil;
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? SessionId { get; set; }
    public string Type { get; set; } = "Info";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
