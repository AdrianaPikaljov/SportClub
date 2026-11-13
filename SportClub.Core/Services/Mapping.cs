using SportClub.Core.Dtos;
using SportClub.Core.Entities;

namespace SportClub.Core.Services;

/// <summary>Entity → DTO conversions. Entities never leave the API.</summary>
public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.FullName, u.Email, u.Role, u.Trainer?.Id);

    public static TrainerDto ToDto(this Trainer t) => new(t.Id, t.FullName, t.Bio, t.Phone, t.UserId);

    public static TrainingTypeDto ToDto(this TrainingType t) => new(t.Id, t.Name, t.Description, t.Intensity);

    public static SessionDto ToDto(this Session s, int booked, int waiting) => new(
        s.Id,
        s.TrainingTypeId,
        s.TrainingType?.Name ?? "",
        s.TrainingType?.Intensity ?? "",
        s.TrainerId,
        s.Trainer?.FullName ?? "",
        DateTime.SpecifyKind(s.StartsAt, DateTimeKind.Utc),
        s.DurationMinutes,
        s.Room,
        s.MaxParticipants,
        booked,
        Math.Max(0, s.MaxParticipants - booked),
        waiting);

    public static BookingDto ToDto(this Booking b) => new(
        b.Id,
        b.SessionId,
        b.Session?.TrainingType?.Name ?? "",
        b.Session?.Trainer?.FullName ?? "",
        b.Session?.Room ?? "",
        DateTime.SpecifyKind(b.Session?.StartsAt ?? default, DateTimeKind.Utc),
        b.Status.ToString(),
        b.Attended,
        DateTime.SpecifyKind(b.CreatedAt, DateTimeKind.Utc));

    public static MonthlyPassDto ToDto(this MonthlyPass p, DateTime nowUtc) => new(
        p.Id, p.UserId, p.User?.FullName ?? "",
        DateTime.SpecifyKind(p.ValidFrom, DateTimeKind.Utc),
        DateTime.SpecifyKind(p.ValidUntil, DateTimeKind.Utc),
        p.VisitsTotal, p.VisitsUsed, p.VisitsLeft,
        p.IsValidAt(nowUtc) && p.VisitsLeft > 0,
        p.PassPlan?.Name,
        p.PricePaid,
        p.PurchasedAt is null ? null : DateTime.SpecifyKind(p.PurchasedAt.Value, DateTimeKind.Utc),
        p.ValidFrom > nowUtc);

    public static PassPlanDto ToDto(this PassPlan p) => new(p.Id, p.Name, p.Description, p.Visits, p.DurationDays, p.Price, p.IsActive);

    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.SessionId, n.Type, n.Message, DateTime.SpecifyKind(n.CreatedAt, DateTimeKind.Utc), n.IsRead);

    public static PagedResult<TOut> Map<TIn, TOut>(this PagedResult<TIn> page, Func<TIn, TOut> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.TotalCount);
}
