using Microsoft.EntityFrameworkCore;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Interfaces;
using SportClub.Infrastructure.Data;

namespace SportClub.Infrastructure.Repositories;

/// <summary>Ühine CRUD kõigile repositorydele.</summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;

    public Repository(AppDbContext db) => Db = db;

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Db.Set<T>().FindAsync(new object[] { id }, ct).AsTask();

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);

    public void Remove(T entity) => Db.Set<T>().Remove(entity);
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<List<User>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var query = Db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s));
        }
        return query.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync(ct);
    }
}

public class TrainerRepository : Repository<Trainer>, ITrainerRepository
{
    public TrainerRepository(AppDbContext db) : base(db) { }

    public Task<List<Trainer>> GetAllAsync(CancellationToken ct = default) =>
        Db.Trainers.Include(t => t.User).AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);

    public Task<Trainer?> GetWithUserAsync(int id, CancellationToken ct = default) =>
        Db.Trainers.Include(t => t.User).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Trainer?> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        Db.Trainers.FirstOrDefaultAsync(t => t.UserId == userId, ct);

    public Task<bool> HasSessionsAfterAsync(int trainerId, DateTime time, CancellationToken ct = default) =>
        Db.Sessions.AnyAsync(s => s.TrainerId == trainerId && s.StartsAt > time, ct);
}

public class TrainingTypeRepository : Repository<TrainingType>, ITrainingTypeRepository
{
    public TrainingTypeRepository(AppDbContext db) : base(db) { }

    public Task<List<TrainingType>> GetAllAsync(CancellationToken ct = default) =>
        Db.TrainingTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);

    public Task<TrainingType?> GetByNameAsync(string name, CancellationToken ct = default) =>
        Db.TrainingTypes.FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower(), ct);

    public Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct = default) =>
        Db.TrainingTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower() && (exceptId == null || t.Id != exceptId), ct);

    public Task<bool> HasSessionsAsync(int typeId, CancellationToken ct = default) =>
        Db.Sessions.AnyAsync(s => s.TrainingTypeId == typeId, ct);
}

public class SessionRepository : Repository<Session>, ISessionRepository
{
    public SessionRepository(AppDbContext db) : base(db) { }

    public Task<Session?> GetDetailedAsync(int id, CancellationToken ct = default) =>
        Db.Sessions.Include(s => s.TrainingType).Include(s => s.Trainer).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Session>> GetListAsync(DateTime? from, DateTime? to, int? typeId, int? trainerId, bool newestFirst,
        CancellationToken ct = default)
    {
        var query = Db.Sessions.Include(s => s.TrainingType).Include(s => s.Trainer).AsNoTracking();
        if (from is not null) query = query.Where(s => s.StartsAt >= from);
        if (to is not null) query = query.Where(s => s.StartsAt < to);
        if (typeId is not null) query = query.Where(s => s.TrainingTypeId == typeId);
        if (trainerId is not null) query = query.Where(s => s.TrainerId == trainerId);
        query = newestFirst ? query.OrderByDescending(s => s.StartsAt) : query.OrderBy(s => s.StartsAt);
        return query.ToListAsync(ct);
    }

    public async Task<Dictionary<int, SessionCounts>> GetCountsAsync(IEnumerable<int> sessionIds, CancellationToken ct = default)
    {
        var ids = sessionIds.ToList();
        var rows = await Db.Bookings
            .Where(b => ids.Contains(b.SessionId))
            .GroupBy(b => b.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Booked = g.Count(b => b.Status == BookingStatus.Booked),
                Waitlisted = g.Count(b => b.Status == BookingStatus.Waitlisted),
                Unmarked = g.Count(b => b.Status == BookingStatus.Booked && b.Attended == null)
            })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.SessionId, r => new SessionCounts(r.Booked, r.Waitlisted, r.Unmarked));
    }

    public Task<int> CountBookedAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status == BookingStatus.Booked, ct);

    public async Task<bool> TrainerBusyAsync(int trainerId, DateTime start, DateTime end, int? exceptSessionId, CancellationToken ct = default)
    {
        // Kestus on igal treeningul erinev, seega kontroll tehakse mälus (treeneri sama päeva treeningud).
        var dayStart = start.Date.AddDays(-1);
        var dayEnd = end.Date.AddDays(1);
        var sameDays = await Db.Sessions.AsNoTracking()
            .Where(s => s.TrainerId == trainerId && s.StartsAt >= dayStart && s.StartsAt < dayEnd
                        && (exceptSessionId == null || s.Id != exceptSessionId))
            .Select(s => new { s.StartsAt, s.DurationMinutes })
            .ToListAsync(ct);
        return sameDays.Any(s => s.StartsAt < end && s.StartsAt.AddMinutes(s.DurationMinutes) > start);
    }

    public Task<List<Session>> GetStartingBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        Db.Sessions.Include(s => s.TrainingType).AsNoTracking()
            .Where(s => s.StartsAt > from && s.StartsAt <= to)
            .OrderBy(s => s.StartsAt)
            .ToListAsync(ct);

    public Task<List<Session>> GetWithWaitlistAsync(CancellationToken ct = default) =>
        Db.Sessions.Include(s => s.TrainingType).Include(s => s.Trainer)
            .Where(s => s.Bookings.Any(b => b.Status == BookingStatus.Waitlisted))
            .OrderBy(s => s.StartsAt)
            .ToListAsync(ct);

    public Task<List<Session>> GetAllForTrainerAsync(int trainerId, CancellationToken ct = default) =>
        Db.Sessions.Where(s => s.TrainerId == trainerId).ToListAsync(ct);
}

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext db) : base(db) { }

    private IQueryable<Booking> WithDetails() =>
        Db.Bookings
            .Include(b => b.User)
            .Include(b => b.MonthlyPass)
            .Include(b => b.Session!).ThenInclude(s => s.TrainingType)
            .Include(b => b.Session!).ThenInclude(s => s.Trainer);

    public Task<Booking?> GetDetailedAsync(int id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Booking?> GetForUserAsync(int sessionId, int userId, CancellationToken ct = default) =>
        Db.Bookings.FirstOrDefaultAsync(b => b.SessionId == sessionId && b.UserId == userId, ct);

    public Task<List<Booking>> GetAllForUserAsync(int userId, CancellationToken ct = default) =>
        WithDetails().AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Session!.StartsAt)
            .ToListAsync(ct);

    public Task<List<Booking>> GetForSessionAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.Include(b => b.User).Include(b => b.MonthlyPass)
            .Where(b => b.SessionId == sessionId)
            .ToListAsync(ct);

    public Task<List<Booking>> GetWaitlistAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.Include(b => b.User)
            .Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Waitlisted)
            .OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .ToListAsync(ct);

    public async Task<PagedResult<Booking>> SearchAsync(string? search, BookingStatus? status, BookingSort sort, int page, int pageSize,
        CancellationToken ct = default)
    {
        var query = WithDetails().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(b => b.User!.FullName.ToLower().Contains(s));
        }
        if (status is not null) query = query.Where(b => b.Status == status);
        query = sort == BookingSort.Vanimad
            ? query.OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            : query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<Booking>(items, page, pageSize, total);
    }

    public Task<List<Booking>> GetWithoutReminderAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.Include(b => b.User).AsNoTracking()
            .Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Booked && b.ReminderSentAt == null)
            .ToListAsync(ct);

    public async Task<List<PopularDto>> GetPopularAsync(DateTime? from, DateTime? to, int? trainerId, CancellationToken ct = default)
    {
        var sessions = Db.Sessions.AsNoTracking();
        if (from is not null) sessions = sessions.Where(s => s.StartsAt >= from);
        if (to is not null) sessions = sessions.Where(s => s.StartsAt < to);
        if (trainerId is not null) sessions = sessions.Where(s => s.TrainerId == trainerId);

        var rows = await sessions
            .Select(s => new
            {
                Type = s.TrainingType!.Name,
                s.Capacity,
                Booked = s.Bookings.Count(b => b.Status == BookingStatus.Booked),
                Attended = s.Bookings.Count(b => b.Status == BookingStatus.Booked && b.Attended == true)
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Type)
            .Select(g => new PopularDto(
                g.Key,
                g.Count(),
                g.Sum(r => r.Booked),
                g.Sum(r => r.Attended),
                (int)Math.Round(g.Average(r => r.Capacity == 0 ? 0 : 100.0 * r.Booked / r.Capacity))))
            .OrderByDescending(p => p.Bookings)
            .ThenByDescending(p => p.FillRate)
            .ToList();
    }
}

public class MonthlyPassRepository : Repository<MonthlyPass>, IMonthlyPassRepository
{
    public MonthlyPassRepository(AppDbContext db) : base(db) { }

    public Task<MonthlyPass?> GetUsableAsync(int userId, DateTime time, CancellationToken ct = default) =>
        Db.MonthlyPasses
            .Where(p => p.UserId == userId && p.ValidFrom <= time && p.ValidUntil >= time && p.VisitsUsed < p.VisitsTotal)
            .OrderBy(p => p.ValidUntil)
            .FirstOrDefaultAsync(ct);

    public Task<List<MonthlyPass>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var query = Db.MonthlyPasses.Include(p => p.User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.User!.FullName.ToLower().Contains(s));
        }
        return query.OrderByDescending(p => p.ValidUntil).ToListAsync(ct);
    }
}

public class PassPlanRepository : Repository<PassPlan>, IPassPlanRepository
{
    public PassPlanRepository(AppDbContext db) : base(db) { }

    public Task<List<PassPlan>> GetActiveAsync(CancellationToken ct = default) =>
        Db.PassPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync(ct);
}

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext db) : base(db) { }
}
