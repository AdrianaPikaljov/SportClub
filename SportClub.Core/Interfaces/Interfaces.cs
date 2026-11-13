using SportClub.Core.Dtos;
using SportClub.Core.Entities;

namespace SportClub.Core.Interfaces;

/// <summary>Generic repository for simple CRUD.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<PagedResult<User>> SearchAsync(ListQuery query, string? role, CancellationToken ct = default);
}

public interface ITrainerRepository : IRepository<Trainer>
{
    Task<PagedResult<Trainer>> SearchAsync(ListQuery query, CancellationToken ct = default);
    Task<Trainer?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<bool> HasSessionsAsync(int trainerId, CancellationToken ct = default);
}

public interface ITrainingTypeRepository : IRepository<TrainingType>
{
    Task<PagedResult<TrainingType>> SearchAsync(ListQuery query, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct = default);
    Task<bool> HasSessionsAsync(int typeId, CancellationToken ct = default);
}

public interface ISessionRepository : IRepository<Session>
{
    /// <summary>Loads session with type and trainer.</summary>
    Task<Session?> GetDetailedAsync(int id, CancellationToken ct = default);
    Task<PagedResult<Session>> SearchAsync(SessionQuery query, CancellationToken ct = default);
    Task<int> CountActiveBookingsAsync(int sessionId, CancellationToken ct = default);
    Task<int> CountWaitlistAsync(int sessionId, CancellationToken ct = default);
    Task<Dictionary<int, (int Booked, int Waiting)>> GetCountsAsync(IEnumerable<int> sessionIds, CancellationToken ct = default);
    Task<List<Session>> GetStartingBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    /// <summary>Future sessions that have people in the waitlist.</summary>
    Task<List<Session>> GetUpcomingWithWaitlistAsync(DateTime nowUtc, CancellationToken ct = default);
}

public interface IBookingRepository : IRepository<Booking>
{
    Task<Booking?> GetWithSessionAsync(int id, CancellationToken ct = default);
    Task<Booking?> GetActiveAsync(int sessionId, int userId, CancellationToken ct = default);
    Task<List<Booking>> GetForUserAsync(int userId, bool upcomingOnly, DateTime nowUtc, CancellationToken ct = default);
    Task<List<Booking>> GetActiveForSessionAsync(int sessionId, CancellationToken ct = default);
    Task<List<Booking>> GetActiveWithoutReminderAsync(int sessionId, CancellationToken ct = default);
    Task<List<PopularTrainingDto>> GetPopularAsync(DateTime? fromUtc, DateTime? toUtc, int top, CancellationToken ct = default);
}

public interface IWaitlistRepository : IRepository<WaitlistEntry>
{
    Task<WaitlistEntry?> GetAsync(int sessionId, int userId, CancellationToken ct = default);
    Task<List<WaitlistEntry>> GetOrderedForSessionAsync(int sessionId, CancellationToken ct = default);
    Task<List<WaitlistEntry>> GetForUserAsync(int userId, DateTime nowUtc, CancellationToken ct = default);
}

public interface IMonthlyPassRepository : IRepository<MonthlyPass>
{
    /// <summary>The user's pass that is valid at the given time and still has visits left.</summary>
    Task<MonthlyPass?> GetUsableAsync(int userId, DateTime atUtc, CancellationToken ct = default);
    Task<List<MonthlyPass>> GetForUserAsync(int userId, CancellationToken ct = default);
    Task<PagedResult<MonthlyPass>> SearchAsync(ListQuery query, CancellationToken ct = default);
    /// <summary>Latest-ending pass of the user that still ends in the future (used to queue a new purchase after it).</summary>
    Task<MonthlyPass?> GetLatestEndingAsync(int userId, DateTime nowUtc, CancellationToken ct = default);
    Task<bool> HasUpcomingAsync(int userId, DateTime nowUtc, CancellationToken ct = default);
    Task<bool> AnyForPlanAsync(int planId, CancellationToken ct = default);
}

public interface IPassPlanRepository : IRepository<PassPlan>
{
    Task<PagedResult<PassPlan>> SearchAsync(ListQuery query, bool onlyActive, CancellationToken ct = default);
}

public interface INotificationRepository : IRepository<Notification>
{
    Task<List<Notification>> GetForUserAsync(int userId, int take, CancellationToken ct = default);
}

/// <summary>Pushes realtime updates to connected clients (implemented with WebSockets in the API).</summary>
public interface ISessionNotifier
{
    Task FreeSpotsChangedAsync(int sessionId, int free, CancellationToken ct = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Information about the logged-in user, passed from controllers to services.</summary>
public record CurrentUser(int Id, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;
    public bool IsTrainer => Role == Roles.Trainer;
}
