using System.ComponentModel.DataAnnotations;

namespace SportClub.Core.Dtos;

// ---------- Common ----------

/// <summary>Üks lehekülg tulemusi: items + page, pageSize, totalCount, totalPages.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Nimekirjade ühised parameetrid: otsing, sorteerimine ja leheküljed.</summary>
public class ListQuery
{
    /// <summary>Otsingusõna.</summary>
    /// <example>jooga</example>
    public string? Search { get; set; }

    /// <summary>Mille järgi sorteerida, nt "name". Miinus ees ("-name") = kahanevalt.</summary>
    /// <example>name</example>
    public string? Sort { get; set; }

    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <example>10</example>
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}

// ---------- Auth ----------

public class LoginRequest
{
    /// <example>klient@sportclub.ee</example>
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    /// <example>Klient123!</example>
    [Required]
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    /// <example>Mari Tamm</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    /// <example>mari.tamm@example.com</example>
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    /// <example>Parool123!</example>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
}

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(int Id, string FullName, string Email, string Role, int? TrainerId);

// ---------- Trainers ----------

public record TrainerDto(int Id, string FullName, string? Bio, string? Phone, int? UserId);

public class TrainerSaveDto
{
    /// <example>Kertu Saar</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    /// <example>Joogaõpetaja, 8 aastat kogemust.</example>
    [StringLength(1000)]
    public string? Bio { get; set; }

    /// <example>+372 5555 1234</example>
    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    /// <summary>Seotud kasutajakonto (roll Trainer), valikuline.</summary>
    public int? UserId { get; set; }
}

// ---------- Training types ----------

public record TrainingTypeDto(int Id, string Name, string? Description, string Intensity);

public class TrainingTypeSaveDto
{
    /// <example>Jooga</example>
    [Required, StringLength(60, MinimumLength = 2)]
    public string Name { get; set; } = "";

    /// <example>Rahulik venitus ja hingamine.</example>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <example>Madal</example>
    [Required, RegularExpression("Madal|Keskmine|Kõrge", ErrorMessage = "Intensiivsus peab olema Madal, Keskmine või Kõrge.")]
    public string Intensity { get; set; } = "Keskmine";
}

// ---------- Sessions (timetable) ----------

public record SessionDto(
    int Id,
    int TrainingTypeId,
    string TrainingTypeName,
    string Intensity,
    int TrainerId,
    string TrainerName,
    DateTime StartsAt,
    int DurationMinutes,
    string Room,
    int MaxParticipants,
    int BookedCount,
    int FreeSpots,
    int WaitlistCount);

public class SessionQuery : ListQuery
{
    /// <summary>Alates (UTC). Vaikimisi tänasest.</summary>
    /// <example>2026-11-16T00:00:00Z</example>
    public DateTime? From { get; set; }

    /// <summary>Kuni (UTC).</summary>
    /// <example>2026-11-23T00:00:00Z</example>
    public DateTime? To { get; set; }

    /// <summary>Treeningutüübi id (1 Jooga, 2 HIIT, 3 Spinning, 4 Pilates, 5 Crossfit).</summary>
    /// <example>1</example>
    public int? Type { get; set; }

    /// <example>2</example>
    public int? TrainerId { get; set; }
}

public class SessionSaveDto : IValidatableObject
{
    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int TrainingTypeId { get; set; }

    /// <example>1</example>
    [Range(1, int.MaxValue)]
    public int TrainerId { get; set; }

    /// <summary>Algusaeg UTC-s (07:00 Eesti aja järgi = 05:00Z).</summary>
    /// <example>2026-11-20T07:00:00Z</example>
    [Required]
    public DateTime StartsAt { get; set; }

    /// <example>60</example>
    [Range(15, 240)]
    public int DurationMinutes { get; set; } = 60;

    /// <example>Saal 2</example>
    [Required, StringLength(50)]
    public string Room { get; set; } = "";

    /// <example>15</example>
    [Range(1, 200)]
    public int MaxParticipants { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1))
            yield return new ValidationResult("Treening ei saa alata minevikus.", new[] { nameof(StartsAt) });
    }
}

public record ParticipantDto(int BookingId, int UserId, string FullName, string Email, bool? Attended);

// ---------- Bookings ----------

public record BookingDto(
    int Id,
    int SessionId,
    string TrainingTypeName,
    string TrainerName,
    string Room,
    DateTime StartsAt,
    string Status,
    bool? Attended,
    DateTime CreatedAt);

public class AttendanceDto
{
    /// <summary>true = kohal, false = puudus.</summary>
    /// <example>true</example>
    [Required]
    public bool? Attended { get; set; }
}

// ---------- Waitlist ----------

public record WaitlistEntryDto(int Id, int SessionId, string TrainingTypeName, DateTime StartsAt, int Position, DateTime CreatedAt);

public record PromoteResultDto(int Promoted, int Skipped);

// ---------- Monthly pass ----------

public record MonthlyPassDto(int Id, int UserId, string UserName, DateTime ValidFrom, DateTime ValidUntil, int VisitsTotal, int VisitsUsed, int VisitsLeft, bool IsActive,
    string? PlanName, decimal? PricePaid, DateTime? PurchasedAt, bool IsUpcoming);

// ---------- Pass plans (what the club sells) ----------

public record PassPlanDto(int Id, string Name, string? Description, int Visits, int DurationDays, decimal Price, bool IsActive);

public class PassPlanSaveDto
{
    /// <example>Kuukaart 12</example>
    [Required, StringLength(60, MinimumLength = 2)]
    public string Name { get; set; } = "";

    /// <example>12 grupitreeningut 30 päeva jooksul.</example>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <example>12</example>
    [Range(1, 100)]
    public int Visits { get; set; } = 12;

    /// <example>30</example>
    [Range(1, 365)]
    public int DurationDays { get; set; } = 30;

    /// <example>49.00</example>
    [Range(typeof(decimal), "0", "1000")]
    public decimal Price { get; set; }

    /// <example>true</example>
    public bool IsActive { get; set; } = true;
}

public class PurchasePassRequest
{
    /// <summary>Ostetava paketi id (vt GET /api/pass-plans).</summary>
    /// <example>3</example>
    [Range(1, int.MaxValue)]
    public int PlanId { get; set; }
}

public class MonthlyPassCreateDto : IValidatableObject
{
    /// <example>3</example>
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    /// <example>2026-11-01T00:00:00Z</example>
    [Required]
    public DateTime ValidFrom { get; set; }

    /// <example>2026-11-30T23:59:59Z</example>
    [Required]
    public DateTime ValidUntil { get; set; }

    /// <example>12</example>
    [Range(1, 100)]
    public int VisitsTotal { get; set; } = 12;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValidUntil <= ValidFrom)
            yield return new ValidationResult("Lõppkuupäev peab olema hilisem kui alguskuupäev.", new[] { nameof(ValidUntil) });
    }
}

// ---------- Notifications / worker ----------

public record StartingSessionDto(int SessionId, string TrainingTypeName, DateTime StartsAt, int PendingReminders);

public class SendReminderRequest
{
    /// <example>12</example>
    [Range(1, int.MaxValue)]
    public int SessionId { get; set; }
}

public record SendReminderResultDto(int SessionId, int Sent);

public record NotificationDto(int Id, int? SessionId, string Type, string Message, DateTime CreatedAt, bool IsRead);

// ---------- Statistics ----------

public record PopularTrainingDto(int TrainingTypeId, string Name, int Sessions, int Bookings, int Attended, double AverageFillPercent);

// ---------- Realtime ----------

/// <summary>WebSocketi sõnum kanalis /ws/sessions.</summary>
public record FreeSpotsChangedMessage(int SessionId, int Free)
{
    public string Type => "FreeSpotsChanged";
}
