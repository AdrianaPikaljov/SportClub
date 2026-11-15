using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface IBookingService
{
    Task<BookingDto> BookAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    Task CancelAsync(int bookingId, CurrentUser user, CancellationToken ct = default);
    Task<List<BookingDto>> GetMyAsync(int userId, bool upcomingOnly, CancellationToken ct = default);
    Task<BookingDto> SetAttendanceAsync(int bookingId, bool attended, CurrentUser user, CancellationToken ct = default);
}

/// <summary>
/// Booking rules:
/// a session has a limited number of spots, a booking uses one visit from the monthly pass,
/// cancelling before the start gives the visit back.
/// </summary>
public class BookingService : IBookingService
{
    private readonly ISessionRepository _sessions;
    private readonly IBookingRepository _bookings;
    private readonly IWaitlistRepository _waitlist;
    private readonly IMonthlyPassRepository _passes;
    private readonly ITrainerRepository _trainers;
    private readonly IUnitOfWork _uow;
    private readonly ISessionNotifier _notifier;
    private readonly IClock _clock;

    public BookingService(ISessionRepository sessions, IBookingRepository bookings, IWaitlistRepository waitlist,
        IMonthlyPassRepository passes, ITrainerRepository trainers, IUnitOfWork uow, ISessionNotifier notifier, IClock clock)
    {
        _sessions = sessions;
        _bookings = bookings;
        _waitlist = waitlist;
        _passes = passes;
        _trainers = trainers;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<BookingDto> BookAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);

        if (session.StartsAt <= now)
            throw new BusinessRuleException("Treening on juba alanud või toimunud.");

        if (await _bookings.GetActiveAsync(sessionId, user.Id, ct) is not null)
            throw new ConflictException("Sul on sellele treeningule juba broneering.", "Juba broneeritud");

        var booked = await _sessions.CountActiveBookingsAsync(sessionId, ct);
        if (booked >= session.MaxParticipants)
            throw new SessionFullException(sessionId);

        var pass = await _passes.GetUsableAsync(user.Id, session.StartsAt, ct)
                   ?? throw new BusinessRuleException("Sul pole treeningu kuupäeval kehtivat kuukaarti, millel oleks külastusi alles.", "Kuukaart puudub");

        var booking = new Booking
        {
            SessionId = sessionId,
            Session = session,
            UserId = user.Id,
            MonthlyPassId = pass.Id,
            CreatedAt = now
        };
        pass.VisitsUsed++;
        await _bookings.AddAsync(booking, ct);

        // If the user was waiting in the queue, they don't need that place any more.
        var entry = await _waitlist.GetAsync(sessionId, user.Id, ct);
        if (entry is not null) _waitlist.Remove(entry);

        await _uow.SaveChangesAsync(ct);
        await _notifier.FreeSpotsChangedAsync(sessionId, session.MaxParticipants - (booked + 1), ct);
        return booking.ToDto();
    }

    public async Task CancelAsync(int bookingId, CurrentUser user, CancellationToken ct = default)
    {
        var booking = await _bookings.GetWithSessionAsync(bookingId, ct) ?? throw new NotFoundException("Broneering", bookingId);

        if (booking.UserId != user.Id && !user.IsAdmin)
            throw new ForbiddenException("Saad tühistada ainult oma broneeringuid.");
        if (booking.Status == BookingStatus.Cancelled)
            throw new ConflictException("Broneering on juba tühistatud.");

        var now = _clock.UtcNow;
        if (booking.Session!.StartsAt <= now)
            throw new BusinessRuleException("Alanud treeningu broneeringut ei saa tühistada.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        if (booking.MonthlyPass is not null && booking.MonthlyPass.VisitsUsed > 0)
            booking.MonthlyPass.VisitsUsed--; // visit goes back to the pass

        await _uow.SaveChangesAsync(ct);

        var booked = await _sessions.CountActiveBookingsAsync(booking.SessionId, ct);
        await _notifier.FreeSpotsChangedAsync(booking.SessionId, booking.Session.MaxParticipants - booked, ct);
        // The freed spot is given to the first person in the waitlist by the Worker (POST /api/waitlist/promote).
    }

    public async Task<List<BookingDto>> GetMyAsync(int userId, bool upcomingOnly, CancellationToken ct = default) =>
        (await _bookings.GetForUserAsync(userId, upcomingOnly, _clock.UtcNow, ct)).Select(b => b.ToDto()).ToList();

    public async Task<BookingDto> SetAttendanceAsync(int bookingId, bool attended, CurrentUser user, CancellationToken ct = default)
    {
        var booking = await _bookings.GetWithSessionAsync(bookingId, ct) ?? throw new NotFoundException("Broneering", bookingId);

        if (!user.IsAdmin)
        {
            var trainer = await _trainers.GetByUserIdAsync(user.Id, ct);
            if (trainer is null || trainer.Id != booking.Session!.TrainerId)
                throw new ForbiddenException("Kohalolu saab märkida ainult selle treeningu treener.");
        }
        if (booking.Status != BookingStatus.Active)
            throw new BusinessRuleException("Tühistatud broneeringule ei saa kohalolu märkida.");
        if (booking.Session!.StartsAt > _clock.UtcNow.AddMinutes(30))
            throw new BusinessRuleException("Kohalolu saab märkida alates 30 minutit enne treeningu algust.");

        booking.Attended = attended;
        await _uow.SaveChangesAsync(ct);
        return booking.ToDto();
    }
}
