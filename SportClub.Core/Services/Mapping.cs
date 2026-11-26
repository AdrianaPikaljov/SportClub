using SportClub.Core.Dtos;
using SportClub.Core.Entities;

namespace SportClub.Core.Services;

/// <summary>Olemid → DTO-d. Olemid API-st välja ei lähe.</summary>
public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.FullName, u.Email, u.Role);

    /// <summary>includeEmail = true ainult adminile (Trainer.User peab olema laaditud).</summary>
    public static TrainerDto ToDto(this Trainer t, bool includeEmail = false) =>
        new(t.Id, t.FullName, t.Specialty, t.Description, t.Phone, includeEmail ? t.User?.Email : null);

    public static TrainingTypeDto ToDto(this TrainingType t) => new(t.Id, t.Name, t.Description);

    public static PassPlanDto ToDto(this PassPlan p) => new(p.Id, p.Name, p.Visits, p.Days, p.Price);

    public static SessionDto ToDto(this Session s, int booked) => new(
        s.Id,
        s.TrainingType?.Name ?? "",
        s.TrainerId,
        s.Trainer?.FullName ?? "",
        ClubTime.Format(s.StartsAt),
        s.DurationMinutes,
        s.Capacity,
        Math.Max(0, s.Capacity - booked));

    /// <summary>position on ainult ootejärjekorras oleval kirjel.</summary>
    public static BookingDto ToDto(this Booking b, int? position) => new(
        b.Id,
        b.SessionId,
        b.Session?.TrainingType?.Name ?? "",
        b.Session?.Trainer?.FullName ?? "",
        b.Session is null ? "" : ClubTime.Format(b.Session.StartsAt),
        b.Status.ToString(),
        b.Status == BookingStatus.Waitlisted ? position : null,
        b.Status == BookingStatus.Booked ? b.Attended : null);

    public static AdminBookingDto ToAdminDto(this Booking b, int? position) => new(
        b.Id,
        b.SessionId,
        b.User?.FullName ?? "",
        b.Session?.TrainingType?.Name ?? "",
        b.Session?.Trainer?.FullName ?? "",
        b.Session is null ? "" : ClubTime.Format(b.Session.StartsAt),
        b.Status.ToString(),
        b.Status == BookingStatus.Waitlisted ? position : null,
        b.Status == BookingStatus.Booked ? b.Attended : null,
        ClubTime.Format(b.CreatedAt));

    public static MyPassDto ToMyDto(this MonthlyPass p) => new(
        p.Id, p.PlanName, p.VisitsLeft, p.VisitsTotal, ClubTime.FormatDate(p.ValidFrom), ClubTime.FormatDate(p.ValidUntil), p.CardText());

    public static PassDto ToDto(this MonthlyPass p, DateTime now) => new(
        p.Id, p.User?.FullName ?? "", p.User?.Email ?? "", p.PlanName, p.VisitsLeft, p.VisitsTotal,
        ClubTime.FormatDate(p.ValidFrom), ClubTime.FormatDate(p.ValidUntil), p.PricePaid, p.IsUsableAt(now), p.CardText());

    /// <summary>"Visa •••• 4242" või null, kui kaardiga ei makstud (nt algandmed).</summary>
    public static string? CardText(this MonthlyPass p) =>
        p.CardLast4 is null ? null : $"{p.CardBrand} •••• {p.CardLast4}";

    public static AttendanceItemDto ToAttendanceDto(this Booking b) =>
        new() { BookingId = b.Id, Name = b.User?.FullName ?? "", Attended = b.Attended };
}
