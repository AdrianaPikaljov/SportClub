using Microsoft.Extensions.Logging;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

// ---------- Monthly passes ----------

public interface IMonthlyPassService
{
    Task<List<MonthlyPassDto>> GetMyAsync(int userId, CancellationToken ct = default);
    Task<PagedResult<MonthlyPassDto>> SearchAsync(ListQuery query, CancellationToken ct = default);
    Task<MonthlyPassDto> CreateAsync(MonthlyPassCreateDto dto, CancellationToken ct = default);
    /// <summary>Client buys a pass from a plan (payment is simulated in this project).</summary>
    Task<MonthlyPassDto> PurchaseAsync(int planId, CurrentUser user, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class MonthlyPassService : IMonthlyPassService
{
    private readonly IMonthlyPassRepository _passes;
    private readonly IPassPlanRepository _plans;
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public MonthlyPassService(IMonthlyPassRepository passes, IPassPlanRepository plans, IUserRepository users,
        INotificationRepository notifications, IUnitOfWork uow, IClock clock)
    {
        _passes = passes;
        _plans = plans;
        _users = users;
        _notifications = notifications;
        _uow = uow;
        _clock = clock;
    }

    public async Task<List<MonthlyPassDto>> GetMyAsync(int userId, CancellationToken ct = default) =>
        (await _passes.GetForUserAsync(userId, ct)).Select(p => p.ToDto(_clock.UtcNow)).ToList();

    public async Task<PagedResult<MonthlyPassDto>> SearchAsync(ListQuery query, CancellationToken ct = default) =>
        (await _passes.SearchAsync(query, ct)).Map(p => p.ToDto(_clock.UtcNow));

    public async Task<MonthlyPassDto> CreateAsync(MonthlyPassCreateDto dto, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(dto.UserId, ct) ?? throw new NotFoundException("Kasutaja", dto.UserId);
        if (user.Role != Roles.Client)
            throw new BusinessRuleException("Kuukaardi saab anda ainult kliendile.");

        var pass = new MonthlyPass
        {
            UserId = user.Id,
            User = user,
            ValidFrom = dto.ValidFrom.ToUniversalTime(),
            ValidUntil = dto.ValidUntil.ToUniversalTime(),
            VisitsTotal = dto.VisitsTotal
        };
        await _passes.AddAsync(pass, ct);
        await _uow.SaveChangesAsync(ct);
        return pass.ToDto(_clock.UtcNow);
    }

    public async Task<MonthlyPassDto> PurchaseAsync(int planId, CurrentUser user, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var plan = await _plans.GetByIdAsync(planId, ct) ?? throw new NotFoundException("Kuukaardi pakett", planId);
        if (!plan.IsActive)
            throw new BusinessRuleException("Seda paketti enam ei müüda.");

        var buyer = await _users.GetByIdAsync(user.Id, ct) ?? throw new NotFoundException("Kasutaja", user.Id);
        if (buyer.Role != Roles.Client)
            throw new BusinessRuleException("Kuukaarti saab osta ainult klient.");

        // Only one pass can wait in line after the current one.
        if (await _passes.HasUpcomingAsync(user.Id, now, ct))
            throw new ConflictException("Sul on juba järgmine kuukaart ootel. Uue saad osta, kui see on alanud.", "Kuukaart on ootel");

        // If the current pass still has visits, the new one starts right after it ends; otherwise today.
        var current = await _passes.GetLatestEndingAsync(user.Id, now, ct);
        var start = current is not null && current.VisitsLeft > 0 ? current.ValidUntil.AddSeconds(1) : now;

        var pass = new MonthlyPass
        {
            UserId = user.Id,
            User = buyer,
            PassPlanId = plan.Id,
            PassPlan = plan,
            ValidFrom = start,
            ValidUntil = start.AddDays(plan.DurationDays),
            VisitsTotal = plan.Visits,
            PricePaid = plan.Price,
            PurchasedAt = now
        };
        await _passes.AddAsync(pass, ct);
        await _notifications.AddAsync(new Notification
        {
            UserId = user.Id,
            Type = "PassPurchased",
            CreatedAt = now,
            Message = $"Ostsid kuukaardi \"{plan.Name}\" ({plan.Visits} külastust, {plan.Price:0.00} €). Kehtib {start:dd.MM}–{pass.ValidUntil:dd.MM.yyyy}."
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return pass.ToDto(now);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var pass = await _passes.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kuukaart", id);
        if (pass.VisitsUsed > 0)
            throw new ConflictException("Kasutatud kuukaarti ei saa kustutada.");
        _passes.Remove(pass);
        await _uow.SaveChangesAsync(ct);
    }
}

// ---------- Pass plans ----------

public interface IPassPlanService
{
    Task<PagedResult<PassPlanDto>> SearchAsync(ListQuery query, bool onlyActive, CancellationToken ct = default);
    Task<PassPlanDto> GetAsync(int id, CancellationToken ct = default);
    Task<PassPlanDto> CreateAsync(PassPlanSaveDto dto, CancellationToken ct = default);
    Task<PassPlanDto> UpdateAsync(int id, PassPlanSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class PassPlanService : IPassPlanService
{
    private readonly IPassPlanRepository _plans;
    private readonly IMonthlyPassRepository _passes;
    private readonly IUnitOfWork _uow;

    public PassPlanService(IPassPlanRepository plans, IMonthlyPassRepository passes, IUnitOfWork uow)
    {
        _plans = plans;
        _passes = passes;
        _uow = uow;
    }

    public async Task<PagedResult<PassPlanDto>> SearchAsync(ListQuery query, bool onlyActive, CancellationToken ct = default) =>
        (await _plans.SearchAsync(query, onlyActive, ct)).Map(p => p.ToDto());

    public async Task<PassPlanDto> GetAsync(int id, CancellationToken ct = default) =>
        (await _plans.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kuukaardi pakett", id)).ToDto();

    public async Task<PassPlanDto> CreateAsync(PassPlanSaveDto dto, CancellationToken ct = default)
    {
        var plan = new PassPlan();
        Apply(plan, dto);
        await _plans.AddAsync(plan, ct);
        await _uow.SaveChangesAsync(ct);
        return plan.ToDto();
    }

    public async Task<PassPlanDto> UpdateAsync(int id, PassPlanSaveDto dto, CancellationToken ct = default)
    {
        var plan = await _plans.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kuukaardi pakett", id);
        Apply(plan, dto); // already sold passes keep their own visits and price
        await _uow.SaveChangesAsync(ct);
        return plan.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var plan = await _plans.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kuukaardi pakett", id);
        if (await _passes.AnyForPlanAsync(id, ct))
            throw new ConflictException("Seda paketti on juba ostetud. Märgi see mitteaktiivseks, et seda enam ei müüdaks.");
        _plans.Remove(plan);
        await _uow.SaveChangesAsync(ct);
    }

    private static void Apply(PassPlan plan, PassPlanSaveDto dto)
    {
        plan.Name = dto.Name.Trim();
        plan.Description = dto.Description;
        plan.Visits = dto.Visits;
        plan.DurationDays = dto.DurationDays;
        plan.Price = Math.Round(dto.Price, 2);
        plan.IsActive = dto.IsActive;
    }
}

// ---------- Notifications (reminders) ----------

public interface INotificationService
{
    /// <summary>Creates a reminder for every active booking of the session that has not got one yet.</summary>
    Task<SendReminderResultDto> SendRemindersAsync(int sessionId, CancellationToken ct = default);
    Task<List<NotificationDto>> GetMyAsync(int userId, CancellationToken ct = default);
}

public class NotificationService : INotificationService
{
    private readonly ISessionRepository _sessions;
    private readonly IBookingRepository _bookings;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ISessionRepository sessions, IBookingRepository bookings, INotificationRepository notifications,
        IUnitOfWork uow, IClock clock, ILogger<NotificationService> logger)
    {
        _sessions = sessions;
        _bookings = bookings;
        _notifications = notifications;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<SendReminderResultDto> SendRemindersAsync(int sessionId, CancellationToken ct = default)
    {
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);
        var now = _clock.UtcNow;
        var pending = await _bookings.GetActiveWithoutReminderAsync(sessionId, ct);

        foreach (var booking in pending)
        {
            booking.ReminderSentAt = now;
            await _notifications.AddAsync(new Notification
            {
                UserId = booking.UserId,
                SessionId = sessionId,
                Type = "Reminder",
                CreatedAt = now,
                Message = $"Meeldetuletus: {session.TrainingType?.Name} algab {session.StartsAt:HH:mm} UTC ({session.Room})."
            }, ct);
        }

        await _uow.SaveChangesAsync(ct);
        if (pending.Count > 0)
            _logger.LogInformation("Sent {Count} reminders for session {SessionId}", pending.Count, sessionId);
        return new SendReminderResultDto(sessionId, pending.Count);
    }

    public async Task<List<NotificationDto>> GetMyAsync(int userId, CancellationToken ct = default) =>
        (await _notifications.GetForUserAsync(userId, 50, ct)).Select(n => n.ToDto()).ToList();
}

// ---------- Statistics ----------

public interface IStatsService
{
    Task<List<PopularTrainingDto>> GetPopularAsync(DateTime? from, DateTime? to, int top, CancellationToken ct = default);
}

public class StatsService : IStatsService
{
    private readonly IBookingRepository _bookings;

    public StatsService(IBookingRepository bookings) => _bookings = bookings;

    public Task<List<PopularTrainingDto>> GetPopularAsync(DateTime? from, DateTime? to, int top, CancellationToken ct = default)
    {
        if (top is < 1 or > 50) throw new BusinessRuleException("Parameeter 'top' peab olema 1–50.");
        return _bookings.GetPopularAsync(from?.ToUniversalTime(), to?.ToUniversalTime(), top, ct);
    }
}

// ---------- Auth & users ----------

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<UserDto> GetUserAsync(int id, CancellationToken ct = default);
    Task<PagedResult<UserDto>> SearchUsersAsync(ListQuery query, string? role, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _uow;

    public AuthService(IUserRepository users, IPasswordService passwords, ITokenService tokens, IUnitOfWork uow)
    {
        _users = users;
        _passwords = passwords;
        _tokens = tokens;
        _uow = uow;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (user is null || !_passwords.Verify(user, request.Password))
            throw new UnauthorizedAccessException("Vale e-post või parool.");

        var (token, expires) = _tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Selle e-postiga kasutaja on juba olemas.");

        var user = new User { FullName = request.FullName.Trim(), Email = email, Role = Roles.Client };
        user.PasswordHash = _passwords.Hash(user, request.Password);
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var (token, expires) = _tokens.CreateToken(user);
        return new AuthResponse(token, expires, user.ToDto());
    }

    public async Task<UserDto> GetUserAsync(int id, CancellationToken ct = default) =>
        (await _users.GetByIdAsync(id, ct) ?? throw new NotFoundException("Kasutaja", id)).ToDto();

    public async Task<PagedResult<UserDto>> SearchUsersAsync(ListQuery query, string? role, CancellationToken ct = default) =>
        (await _users.SearchAsync(query, role, ct)).Map(u => u.ToDto());
}
