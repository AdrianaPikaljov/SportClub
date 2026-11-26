using Microsoft.Extensions.Logging;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

// ---------- Ootejärjekord (Worker) ----------

public interface IWaitlistService
{
    /// <summary>Viib vabanenud kohtadele järjekorras esimesed. Worker kutsub seda iga minuti järel.</summary>
    Task<PromoteResultDto> PromoteAsync(CancellationToken ct = default);
}

/// <summary>
/// Järjekorra reeglid:
/// - kohale viiakse ainult treeningutele, mis algavad rohkem kui tunni pärast;
/// - tund enne algust eemaldatakse kõik, kes on veel järjekorras;
/// - kui esimesel pole kehtivat kuukaarti, eemaldatakse ta järjekorrast ja võetakse järgmine.
/// </summary>
public class WaitlistService : IWaitlistService
{
    private readonly ISessionRepository _sessions;
    private readonly IBookingRepository _bookings;
    private readonly IMonthlyPassRepository _passes;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;
    private readonly ISessionNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(ISessionRepository sessions, IBookingRepository bookings, IMonthlyPassRepository passes,
        INotificationRepository notifications, IUnitOfWork uow, ISessionNotifier notifier, IClock clock, ILogger<WaitlistService> logger)
    {
        _sessions = sessions;
        _bookings = bookings;
        _passes = passes;
        _notifications = notifications;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PromoteResultDto> PromoteAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var promoted = new List<PromotedDto>();
        var removed = 0;

        foreach (var session in await _sessions.GetWithWaitlistAsync(ct))
        {
            var queue = await _bookings.GetWaitlistAsync(session.Id, ct);

            // Tund enne algust: kõik ootajad eemaldatakse.
            if (session.StartsAt <= now.AddMinutes(BookingService.WaitlistClosesMinutesBefore))
            {
                foreach (var entry in queue) _bookings.Remove(entry);
                removed += queue.Count;
                await _uow.SaveChangesAsync(ct);
                _logger.LogInformation("Waitlist: {Count} removed from session {SessionId} (starts soon)", queue.Count, session.Id);
                continue;
            }

            var free = session.Capacity - await _sessions.CountBookedAsync(session.Id, ct);
            if (free <= 0) continue;

            foreach (var entry in queue)
            {
                if (free <= 0) break;

                var pass = await _passes.GetUsableAsync(entry.UserId, session.StartsAt, ct);
                if (pass is null)
                {
                    _bookings.Remove(entry);
                    removed++;
                    await _uow.SaveChangesAsync(ct);
                    _logger.LogInformation("Waitlist: user {UserId} removed from session {SessionId} (no pass)", entry.UserId, session.Id);
                    continue;
                }

                entry.Status = BookingStatus.Booked;
                entry.MonthlyPassId = pass.Id;
                pass.VisitsUsed++;
                await _notifications.AddAsync(new Notification
                {
                    UserId = entry.UserId,
                    BookingId = entry.Id,
                    SessionId = session.Id,
                    Type = "WaitlistPromoted",
                    CreatedAt = now,
                    Message = $"Sind viidi järjekorrast treeningule {session.TrainingType?.Name} {session.StartsAt:dd.MM HH:mm}."
                }, ct);
                // Salvestame kohe, et sama kuukaarti ei kasutataks ühes ringis kaks korda üle.
                await _uow.SaveChangesAsync(ct);

                free--;
                promoted.Add(new PromotedDto(entry.Id, session.Id, entry.User?.FullName ?? ""));
                _logger.LogInformation("Waitlist: user {UserId} promoted to session {SessionId}", entry.UserId, session.Id);
            }

            await _notifier.FreeSpotsChangedAsync(session.Id, Math.Max(0, free), ct);
        }

        return new PromoteResultDto(promoted, removed);
    }
}

// ---------- Kuukaardid ----------

public interface IMonthlyPassService
{
    Task<MyPassDto> GetMyAsync(int userId, CancellationToken ct = default);
    Task<MyPassDto> BuyAsync(int planId, CurrentUser user, CancellationToken ct = default);
    Task<List<PassDto>> SearchAsync(string? search, CancellationToken ct = default);
}

public class MonthlyPassService : IMonthlyPassService
{
    private readonly IMonthlyPassRepository _passes;
    private readonly IPassPlanRepository _plans;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public MonthlyPassService(IMonthlyPassRepository passes, IPassPlanRepository plans, IUnitOfWork uow, IClock clock)
    {
        _passes = passes;
        _plans = plans;
        _uow = uow;
        _clock = clock;
    }

    public async Task<MyPassDto> GetMyAsync(int userId, CancellationToken ct = default)
    {
        var pass = await _passes.GetUsableAsync(userId, _clock.Now, ct)
                   ?? throw new NotFoundException("Sul ei ole aktiivset kuukaarti.");
        return pass.ToMyDto();
    }

    /// <summary>Ostab kuukaardi. Kehtib tänasest. Korraga saab olla ainult üks aktiivne kuukaart.</summary>
    public async Task<MyPassDto> BuyAsync(int planId, CurrentUser user, CancellationToken ct = default)
    {
        var plan = await _plans.GetByIdAsync(planId, ct);
        if (plan is null || !plan.IsActive) throw new NotFoundException("Tariif", planId);

        var now = _clock.Now;
        if (await _passes.GetUsableAsync(user.Id, now, ct) is not null)
            throw new ConflictException("Sul on juba aktiivne kuukaart. Uue saad osta, kui külastused on otsas või kaart on aegunud.",
                "Kuukaart on juba olemas");

        var pass = new MonthlyPass
        {
            UserId = user.Id,
            PassPlanId = plan.Id,
            PlanName = plan.Name,
            PricePaid = plan.Price,
            ValidFrom = now.Date,
            ValidUntil = now.Date.AddDays(plan.Days + 1).AddSeconds(-1), // kehtib kuni viimase päeva lõpuni
            VisitsTotal = plan.Visits
        };
        await _passes.AddAsync(pass, ct);
        await _uow.SaveChangesAsync(ct);
        return pass.ToMyDto();
    }

    public async Task<List<PassDto>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var now = _clock.Now;
        return (await _passes.SearchAsync(search, ct)).Select(p => p.ToDto(now)).ToList();
    }
}

// ---------- Meeldetuletused (Worker) ----------

public interface INotificationService
{
    Task<NotificationDto> SendReminderAsync(int bookingId, CancellationToken ct = default);
}

/// <summary>
/// Päris e-kirja ei saadeta: meeldetuletus salvestatakse tabelisse Notifications ja kirjutatakse logisse.
/// Broneering märgitakse "meelde tuletatud", et sama inimene ei saaks seda mitu korda.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IBookingRepository _bookings;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IBookingRepository bookings, INotificationRepository notifications, IUnitOfWork uow,
        IClock clock, ILogger<NotificationService> logger)
    {
        _bookings = bookings;
        _notifications = notifications;
        _uow = uow;
        _clock = clock;
        _logger = logger;
    }

    public async Task<NotificationDto> SendReminderAsync(int bookingId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetDetailedAsync(bookingId, ct) ?? throw new NotFoundException("Broneering", bookingId);
        if (booking.Status != BookingStatus.Booked)
            throw new BusinessRuleException("Ootejärjekorras olijale meeldetuletust ei saadeta.");
        if (booking.ReminderSentAt is not null)
            throw new ConflictException("Sellele broneeringule on meeldetuletus juba saadetud.", "Juba saadetud");

        var now = _clock.Now;
        var session = booking.Session!;
        var notification = new Notification
        {
            UserId = booking.UserId,
            BookingId = booking.Id,
            SessionId = session.Id,
            Type = "Reminder",
            CreatedAt = now,
            Message = $"Meeldetuletus: {session.TrainingType?.Name} algab {session.StartsAt:dd.MM HH:mm}."
        };
        booking.ReminderSentAt = now;
        await _notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Reminder → {Email}: {Message}", booking.User?.Email, notification.Message);
        return new NotificationDto(notification.Id, booking.Id, booking.User?.FullName ?? "", notification.Message, ClubTime.Format(now));
    }
}

// ---------- Statistika ----------

public interface IStatsService
{
    Task<List<PopularDto>> GetPopularAsync(string? from, string? to, CurrentUser user, CancellationToken ct = default);
}

/// <summary>Treener näeb statistikat ainult oma treeningute kohta, admin kogu klubi kohta.</summary>
public class StatsService : IStatsService
{
    private readonly IBookingRepository _bookings;
    private readonly ITrainerRepository _trainers;

    public StatsService(IBookingRepository bookings, ITrainerRepository trainers)
    {
        _bookings = bookings;
        _trainers = trainers;
    }

    public async Task<List<PopularDto>> GetPopularAsync(string? from, string? to, CurrentUser user, CancellationToken ct = default)
    {
        var f = ClubTime.ParseDate(from, "from");
        var t = ClubTime.ParseDate(to, "to");
        if (f is not null && t is not null && f > t)
            throw new BusinessRuleException("Kuupäev 'from' ei saa olla hilisem kui 'to'.");

        int? trainerId = null;
        if (!user.IsAdmin)
        {
            var trainer = await _trainers.GetByUserIdAsync(user.Id, ct)
                          ?? throw new ForbiddenException("Sinu kontoga pole seotud ühtegi treenerit.");
            trainerId = trainer.Id;
        }
        return await _bookings.GetPopularAsync(f, t?.AddDays(1), trainerId, ct);
    }
}
