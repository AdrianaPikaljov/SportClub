using System.Globalization;

namespace SportClub.Maui.Models;

// API DTO-de koopiad (mobiilirakendus ei viita backendi projektidele).
// Ajad tulevad API-st Eesti aja järgi tekstina: "2026-11-27 18:00", kuupäevad "2026-11-27".

public record LoginResponse(string Token, string Role, string Name);

public record UserDto(int Id, string Name, string Email, string Role);

/// <summary>Sisseloginud kasutaja, mida rakendus enda juures hoiab.</summary>
public record SignedInUser(string Name, string Email, string Role);

public record TrainingTypeDto(int Id, string Name, string? Description);

public record SessionDto(int Id, string Type, int TrainerId, string Trainer, string StartsAt, int DurationMinutes, int Capacity, int Free)
{
    public DateTime Start => ApiTime.Parse(StartsAt);
}

public record TrainerSessionDto(int Id, string Type, string StartsAt, int DurationMinutes, int Capacity, int Booked, int Waitlisted, string Status)
{
    public DateTime Start => ApiTime.Parse(StartsAt);
}

/// <summary>status: Booked või Waitlisted. position = koht järjekorras.</summary>
public record BookingDto(int Id, int SessionId, string Type, string Trainer, string StartsAt, string Status, int? Position, bool? Attended)
{
    public DateTime Start => ApiTime.Parse(StartsAt);
}

public record MonthlyPassDto(int Id, string Plan, int VisitsLeft, int VisitsTotal, string ValidFrom, string ValidUntil)
{
    /// <summary>GET /api/passes/my tagastab ainult aktiivse kaardi.</summary>
    public bool IsActive => VisitsLeft > 0;
    public DateTime From => ApiTime.ParseDate(ValidFrom);
    public DateTime Until => ApiTime.ParseDate(ValidUntil);
}

public record PassPlanDto(int Id, string Name, int Visits, int Days, decimal Price);

public record AttendanceItemDto(int BookingId, string? Name, bool? Attended);

public record PopularDto(string Type, int Sessions, int Bookings, int Attended, int FillRate);

public record ProblemDetailsDto(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors, int? Position);

public record FreeSpotsChangedMessage(string Type, int SessionId, int Free);

/// <summary>Registreerumise tulemus: kas said koha või läksid ootejärjekorda.</summary>
public record BookResult(bool Waitlisted, int? Position);

public static class ApiTime
{
    public static DateTime Parse(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static DateTime ParseDate(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
