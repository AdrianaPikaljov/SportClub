using Microsoft.Extensions.Logging;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface IWaitlistService
{
    Task<WaitlistEntryDto> JoinAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    Task LeaveAsync(int entryId, CurrentUser user, CancellationToken ct = default);
    Task<List<WaitlistEntryDto>> GetMyAsync(int userId, CancellationToken ct = default);
    /// <summary>Moves the first person in each queue to a freed spot. Called by the Worker every minute.</summary>
    Task<PromoteResultDto> PromoteAsync(CancellationToken ct = default);
}

public class WaitlistService : IWaitlistService
{
    private readonly ISessionRepository _sessions;
    private readonly IBookingRepository _bookings;
    private readonly IWaitlistRepository _waitlist;
    private readonly IMonthlyPassRepository _passes;
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _uow;
    private readonly ISessionNotifier _notifier;
    private readonly IClock _clock;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(ISessionRepository sessions, IBookingRepository bookings, IWaitlistRepository waitlist,
        IMonthlyPassRepository passes, INotificationRepository notifications, IUnitOfWork uow,
        ISessionNotifier notifier, IClock clock, ILogger<WaitlistService> logger)
    {
        _sessions = sessions;
        _bookings = bookings;
        _waitlist = waitlist;
        _passes = passes;
        _notifications = notifications;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
        _logger = logger;
    }

    public async Task<WaitlistEntryDto> JoinAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);

        if (session.StartsAt <= now)
            throw new BusinessRuleException("Treening on juba alanud või toimunud.");
        if (await _bookings.GetActiveAsync(sessionId, user.Id, ct) is not null)
            throw new ConflictException("Sul on sellele treeningule juba broneering.");
        if (await _waitlist.GetAsync(sessionId, user.Id, ct) is not null)
            throw new ConflictException("Oled juba selle treeningu ootejärjekorras.");

        var booked = await _sessions.CountActiveBookingsAsync(sessionId, ct);
        if (booked < session.MaxParticipants)
            throw new ConflictException("Treeningul on vabu kohti – broneeri koht otse.", "Vabu kohti on");

        if (await _passes.GetUsableAsync(user.Id, session.StartsAt, ct) is null)
            throw new BusinessRuleException("Sul pole treeningu kuupäeval kehtivat kuukaarti, millel oleks külastusi alles.", "Kuukaart puudub");

        var entry = new WaitlistEntry { SessionId = sessionId, Session = session, UserId = user.Id, CreatedAt = now };
        await _waitlist.AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);

        var position = (await _waitlist.GetOrderedForSessionAsync(sessionId, ct)).FindIndex(e => e.Id == entry.Id) + 1;
        return new WaitlistEntryDto(entry.Id, sessionId, session.TrainingType?.Name ?? "",
            DateTime.SpecifyKind(session.StartsAt, DateTimeKind.Utc), position, entry.CreatedAt);
    }

    public async Task LeaveAsync(int entryId, CurrentUser user, CancellationToken ct = default)
    {
        var entry = await _waitlist.GetByIdAsync(entryId, ct) ?? throw new NotFoundException("Ootejärjekorra kirje", entryId);
        if (entry.UserId != user.Id && !user.IsAdmin)
            throw new ForbiddenException("Saad lahkuda ainult enda ootejärjekorra kohalt.");
        _waitlist.Remove(entry);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<WaitlistEntryDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        var entries = await _waitlist.GetForUserAsync(userId, _clock.UtcNow, ct);
        var result = new List<WaitlistEntryDto>();
        foreach (var e in entries)
        {
            var queue = await _waitlist.GetOrderedForSessionAsync(e.SessionId, ct);
            result.Add(new WaitlistEntryDto(e.Id, e.SessionId, e.Session?.TrainingType?.Name ?? "",
                DateTime.SpecifyKind(e.Session!.StartsAt, DateTimeKind.Utc),
                queue.FindIndex(q => q.Id == e.Id) + 1, e.CreatedAt));
        }
        return result;
    }

    public async Task<PromoteResultDto> PromoteAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        int promoted = 0, skipped = 0;

        foreach (var session in await _sessions.GetUpcomingWithWaitlistAsync(now, ct))
        {
            var booked = await _sessions.CountActiveBookingsAsync(session.Id, ct);
            var free = session.MaxParticipants - booked;
            if (free <= 0) continue;

            var queue = await _waitlist.GetOrderedForSessionAsync(session.Id, ct);
            foreach (var entry in queue)
            {
                if (free <= 0) break;

                var pass = await _passes.GetUsableAsync(entry.UserId, session.StartsAt, ct);
                _waitlist.Remove(entry);

                if (pass is null)
                {
                    skipped++;
                    await _notifications.AddAsync(new Notification
                    {
                        UserId = entry.UserId, SessionId = session.Id, Type = "WaitlistSkipped", CreatedAt = now,
                        Message = $"Treeningul {session.TrainingType?.Name} vabanes koht, aga sul polnud kehtivat kuukaarti."
                    }, ct);
                    continue;
                }

                pass.VisitsUsed++;
                await _bookings.AddAsync(new Booking
                {
                    SessionId = session.Id, UserId = entry.UserId, MonthlyPassId = pass.Id, CreatedAt = now
                }, ct);
                await _notifications.AddAsync(new Notification
                {
                    UserId = entry.UserId, SessionId = session.Id, Type = "WaitlistPromoted", CreatedAt = now,
                    Message = $"Said koha treeningule {session.TrainingType?.Name} ({session.StartsAt:dd.MM HH:mm} UTC)!"
                }, ct);
                free--;
                promoted++;
                _logger.LogInformation("Waitlist: user {UserId} promoted to session {SessionId}", entry.UserId, session.Id);
            }

            await _uow.SaveChangesAsync(ct);
            await _notifier.FreeSpotsChangedAsync(session.Id, Math.Max(0, free), ct);
        }

        return new PromoteResultDto(promoted, skipped);
    }
}
