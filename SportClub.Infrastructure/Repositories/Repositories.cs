using Microsoft.EntityFrameworkCore;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Interfaces;
using SportClub.Infrastructure.Data;

namespace SportClub.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Db;

    public Repository(AppDbContext db) => Db = db;

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Db.Set<T>().FindAsync(new object[] { id }, ct).AsTask();

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Db.Set<T>().AddAsync(entity, ct);

    public void Remove(T entity) => Db.Set<T>().Remove(entity);

    protected static async Task<PagedResult<TItem>> PageAsync<TItem>(IQueryable<TItem> query, ListQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<TItem>(items, page, size, total);
    }

    /// <summary>"name" → ascending, "-name" → descending.</summary>
    protected static (string Field, bool Desc) ParseSort(string? sort, string defaultField)
    {
        if (string.IsNullOrWhiteSpace(sort)) return (defaultField, false);
        var desc = sort.StartsWith('-');
        return (sort.TrimStart('-').ToLowerInvariant(), desc);
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public override Task<User?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Db.Users.Include(u => u.Trainer).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Db.Users.Include(u => u.Trainer).FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<PagedResult<User>> SearchAsync(ListQuery q, string? role, CancellationToken ct = default)
    {
        var query = Db.Users.Include(u => u.Trainer).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(s) || u.Email.ToLower().Contains(s));
        }
        var (field, desc) = ParseSort(q.Sort, "name");
        query = field switch
        {
            "email" => desc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "role" => desc ? query.OrderByDescending(u => u.Role) : query.OrderBy(u => u.Role),
            _ => desc ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName)
        };
        return PageAsync(query, q, ct);
    }
}

public class TrainerRepository : Repository<Trainer>, ITrainerRepository
{
    public TrainerRepository(AppDbContext db) : base(db) { }

    public Task<PagedResult<Trainer>> SearchAsync(ListQuery q, CancellationToken ct = default)
    {
        var query = Db.Trainers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(t => t.FullName.ToLower().Contains(s) || (t.Bio != null && t.Bio.ToLower().Contains(s)));
        }
        var (field, desc) = ParseSort(q.Sort, "name");
        query = field switch
        {
            "id" => desc ? query.OrderByDescending(t => t.Id) : query.OrderBy(t => t.Id),
            _ => desc ? query.OrderByDescending(t => t.FullName) : query.OrderBy(t => t.FullName)
        };
        return PageAsync(query, q, ct);
    }

    public Task<Trainer?> GetByUserIdAsync(int userId, CancellationToken ct = default) =>
        Db.Trainers.FirstOrDefaultAsync(t => t.UserId == userId, ct);

    public Task<bool> HasSessionsAsync(int trainerId, CancellationToken ct = default) =>
        Db.Sessions.AnyAsync(s => s.TrainerId == trainerId, ct);
}

public class TrainingTypeRepository : Repository<TrainingType>, ITrainingTypeRepository
{
    public TrainingTypeRepository(AppDbContext db) : base(db) { }

    public Task<PagedResult<TrainingType>> SearchAsync(ListQuery q, CancellationToken ct = default)
    {
        var query = Db.TrainingTypes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(s) || (t.Description != null && t.Description.ToLower().Contains(s)));
        }
        var (field, desc) = ParseSort(q.Sort, "name");
        query = field switch
        {
            "intensity" => desc ? query.OrderByDescending(t => t.Intensity) : query.OrderBy(t => t.Intensity),
            "id" => desc ? query.OrderByDescending(t => t.Id) : query.OrderBy(t => t.Id),
            _ => desc ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name)
        };
        return PageAsync(query, q, ct);
    }

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

    public Task<PagedResult<Session>> SearchAsync(SessionQuery q, CancellationToken ct = default)
    {
        var query = Db.Sessions.Include(s => s.TrainingType).Include(s => s.Trainer).AsNoTracking().AsQueryable();

        var from = (q.From ?? DateTime.UtcNow.Date).ToUniversalTime();
        query = query.Where(s => s.StartsAt >= from);
        if (q.To is not null)
        {
            var to = q.To.Value.ToUniversalTime();
            query = query.Where(s => s.StartsAt < to);
        }
        if (q.Type is not null) query = query.Where(s => s.TrainingTypeId == q.Type);
        if (q.TrainerId is not null) query = query.Where(s => s.TrainerId == q.TrainerId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(x => x.TrainingType!.Name.ToLower().Contains(s)
                                     || x.Trainer!.FullName.ToLower().Contains(s)
                                     || x.Room.ToLower().Contains(s));
        }

        var (field, desc) = ParseSort(q.Sort, "startsat");
        query = field switch
        {
            "type" => desc ? query.OrderByDescending(s => s.TrainingType!.Name) : query.OrderBy(s => s.TrainingType!.Name),
            "trainer" => desc ? query.OrderByDescending(s => s.Trainer!.FullName) : query.OrderBy(s => s.Trainer!.FullName),
            "room" => desc ? query.OrderByDescending(s => s.Room) : query.OrderBy(s => s.Room),
            _ => desc ? query.OrderByDescending(s => s.StartsAt) : query.OrderBy(s => s.StartsAt)
        };
        return PageAsync(query, q, ct);
    }

    public Task<int> CountActiveBookingsAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.CountAsync(b => b.SessionId == sessionId && b.Status == BookingStatus.Active, ct);

    public Task<int> CountWaitlistAsync(int sessionId, CancellationToken ct = default) =>
        Db.WaitlistEntries.CountAsync(w => w.SessionId == sessionId, ct);

    public async Task<Dictionary<int, (int Booked, int Waiting)>> GetCountsAsync(IEnumerable<int> sessionIds, CancellationToken ct = default)
    {
        var ids = sessionIds.ToList();
        var booked = await Db.Bookings
            .Where(b => ids.Contains(b.SessionId) && b.Status == BookingStatus.Active)
            .GroupBy(b => b.SessionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var waiting = await Db.WaitlistEntries
            .Where(w => ids.Contains(w.SessionId))
            .GroupBy(w => w.SessionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return ids.ToDictionary(id => id, id => (booked.GetValueOrDefault(id), waiting.GetValueOrDefault(id)));
    }

    public Task<List<Session>> GetStartingBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        Db.Sessions.Include(s => s.TrainingType)
            .Where(s => s.StartsAt > fromUtc && s.StartsAt <= toUtc)
            .OrderBy(s => s.StartsAt)
            .ToListAsync(ct);

    public Task<List<Session>> GetUpcomingWithWaitlistAsync(DateTime nowUtc, CancellationToken ct = default) =>
        Db.Sessions.Include(s => s.TrainingType)
            .Where(s => s.StartsAt > nowUtc && s.WaitlistEntries.Any())
            .ToListAsync(ct);
}

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext db) : base(db) { }

    private IQueryable<Booking> WithDetails() =>
        Db.Bookings.Include(b => b.Session!).ThenInclude(s => s.TrainingType)
                   .Include(b => b.Session!).ThenInclude(s => s.Trainer);

    public Task<Booking?> GetWithSessionAsync(int id, CancellationToken ct = default) =>
        WithDetails().Include(b => b.MonthlyPass).FirstOrDefaultAsync(b => b.Id == id, ct);

    public Task<Booking?> GetActiveAsync(int sessionId, int userId, CancellationToken ct = default) =>
        Db.Bookings.FirstOrDefaultAsync(b => b.SessionId == sessionId && b.UserId == userId && b.Status == BookingStatus.Active, ct);

    public Task<List<Booking>> GetForUserAsync(int userId, bool upcomingOnly, DateTime nowUtc, CancellationToken ct = default)
    {
        var query = WithDetails().AsNoTracking().Where(b => b.UserId == userId);
        query = upcomingOnly
            ? query.Where(b => b.Status == BookingStatus.Active && b.Session!.StartsAt > nowUtc).OrderBy(b => b.Session!.StartsAt)
            : query.OrderByDescending(b => b.Session!.StartsAt);
        return query.Take(100).ToListAsync(ct);
    }

    public Task<List<Booking>> GetActiveForSessionAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.Include(b => b.User).Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Active).ToListAsync(ct);

    public Task<List<Booking>> GetActiveWithoutReminderAsync(int sessionId, CancellationToken ct = default) =>
        Db.Bookings.Where(b => b.SessionId == sessionId && b.Status == BookingStatus.Active && b.ReminderSentAt == null).ToListAsync(ct);

    public async Task<List<PopularTrainingDto>> GetPopularAsync(DateTime? fromUtc, DateTime? toUtc, int top, CancellationToken ct = default)
    {
        var sessions = Db.Sessions.AsNoTracking().AsQueryable();
        if (fromUtc is not null) sessions = sessions.Where(s => s.StartsAt >= fromUtc);
        if (toUtc is not null) sessions = sessions.Where(s => s.StartsAt < toUtc);

        var rows = await sessions
            .Select(s => new
            {
                s.TrainingTypeId,
                TypeName = s.TrainingType!.Name,
                s.MaxParticipants,
                Booked = s.Bookings.Count(b => b.Status == BookingStatus.Active),
                Attended = s.Bookings.Count(b => b.Status == BookingStatus.Active && b.Attended == true)
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.TrainingTypeId, r.TypeName })
            .Select(g => new PopularTrainingDto(
                g.Key.TrainingTypeId,
                g.Key.TypeName,
                g.Count(),
                g.Sum(r => r.Booked),
                g.Sum(r => r.Attended),
                Math.Round(g.Average(r => r.MaxParticipants == 0 ? 0 : 100.0 * r.Booked / r.MaxParticipants), 1)))
            .OrderByDescending(p => p.Bookings)
            .ThenByDescending(p => p.AverageFillPercent)
            .Take(top)
            .ToList();
    }
}

public class WaitlistRepository : Repository<WaitlistEntry>, IWaitlistRepository
{
    public WaitlistRepository(AppDbContext db) : base(db) { }

    public Task<WaitlistEntry?> GetAsync(int sessionId, int userId, CancellationToken ct = default) =>
        Db.WaitlistEntries.FirstOrDefaultAsync(w => w.SessionId == sessionId && w.UserId == userId, ct);

    public Task<List<WaitlistEntry>> GetOrderedForSessionAsync(int sessionId, CancellationToken ct = default) =>
        Db.WaitlistEntries.Where(w => w.SessionId == sessionId).OrderBy(w => w.CreatedAt).ThenBy(w => w.Id).ToListAsync(ct);

    public Task<List<WaitlistEntry>> GetForUserAsync(int userId, DateTime nowUtc, CancellationToken ct = default) =>
        Db.WaitlistEntries.Include(w => w.Session!).ThenInclude(s => s.TrainingType)
            .Where(w => w.UserId == userId && w.Session!.StartsAt > nowUtc)
            .OrderBy(w => w.Session!.StartsAt)
            .ToListAsync(ct);
}

public class MonthlyPassRepository : Repository<MonthlyPass>, IMonthlyPassRepository
{
    public MonthlyPassRepository(AppDbContext db) : base(db) { }

    public Task<MonthlyPass?> GetUsableAsync(int userId, DateTime atUtc, CancellationToken ct = default) =>
        Db.MonthlyPasses
            .Where(p => p.UserId == userId && p.ValidFrom <= atUtc && p.ValidUntil >= atUtc && p.VisitsUsed < p.VisitsTotal)
            .OrderBy(p => p.ValidUntil)
            .FirstOrDefaultAsync(ct);

    public Task<MonthlyPass?> GetLatestEndingAsync(int userId, DateTime nowUtc, CancellationToken ct = default) =>
        Db.MonthlyPasses.Where(p => p.UserId == userId && p.ValidUntil >= nowUtc)
            .OrderByDescending(p => p.ValidUntil).FirstOrDefaultAsync(ct);

    public Task<bool> HasUpcomingAsync(int userId, DateTime nowUtc, CancellationToken ct = default) =>
        Db.MonthlyPasses.AnyAsync(p => p.UserId == userId && p.ValidFrom > nowUtc, ct);

    public Task<bool> AnyForPlanAsync(int planId, CancellationToken ct = default) =>
        Db.MonthlyPasses.AnyAsync(p => p.PassPlanId == planId, ct);

    public Task<List<MonthlyPass>> GetForUserAsync(int userId, CancellationToken ct = default) =>
        Db.MonthlyPasses.Include(p => p.User).Include(p => p.PassPlan).AsNoTracking()
            .Where(p => p.UserId == userId).OrderByDescending(p => p.ValidUntil).ToListAsync(ct);

    public Task<PagedResult<MonthlyPass>> SearchAsync(ListQuery q, CancellationToken ct = default)
    {
        var query = Db.MonthlyPasses.Include(p => p.User).Include(p => p.PassPlan).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(p => p.User!.FullName.ToLower().Contains(s) || p.User.Email.ToLower().Contains(s));
        }
        var (field, desc) = ParseSort(q.Sort, "-validuntil");
        if (string.IsNullOrWhiteSpace(q.Sort)) desc = true;
        query = field switch
        {
            "user" => desc ? query.OrderByDescending(p => p.User!.FullName) : query.OrderBy(p => p.User!.FullName),
            _ => desc ? query.OrderByDescending(p => p.ValidUntil) : query.OrderBy(p => p.ValidUntil)
        };
        return PageAsync(query, q, ct);
    }
}

public class PassPlanRepository : Repository<PassPlan>, IPassPlanRepository
{
    public PassPlanRepository(AppDbContext db) : base(db) { }

    public Task<PagedResult<PassPlan>> SearchAsync(ListQuery q, bool onlyActive, CancellationToken ct = default)
    {
        var query = Db.PassPlans.AsNoTracking().AsQueryable();
        if (onlyActive) query = query.Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s));
        }
        var (field, desc) = ParseSort(q.Sort, "price");
        query = field switch
        {
            "name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "visits" => desc ? query.OrderByDescending(p => p.Visits) : query.OrderBy(p => p.Visits),
            _ => desc ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price)
        };
        return PageAsync(query, q, ct);
    }
}

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext db) : base(db) { }

    public Task<List<Notification>> GetForUserAsync(int userId, int take, CancellationToken ct = default) =>
        Db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync(ct);
}
