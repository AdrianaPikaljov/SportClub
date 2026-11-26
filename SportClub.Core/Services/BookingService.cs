using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface IBookingService
{
    /// <summary>Broneerib koha. Kui kohti pole, paneb kliendi ootejärjekorda (status = Waitlisted).</summary>
    Task<BookingDto> BookAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    /// <summary>Paneb täis treeningu ootejärjekorda (ülesande nõue, sama mis BookAsync täis treeningul).</summary>
    Task<WaitlistDto> JoinWaitlistAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    Task<List<BookingDto>> GetMyAsync(int userId, CancellationToken ct = default);
    /// <summary>Tühistab broneeringu või eemaldab ootejärjekorrast.</summary>
    Task CancelAsync(int bookingId, CurrentUser user, CancellationToken ct = default);
    Task<PagedResult<AdminBookingDto>> SearchAsync(string? search, BookingStatus? status, BookingSort sort, int page, CancellationToken ct = default);
    Task<AttendanceItemDto> SetAttendanceAsync(int bookingId, bool attended, CurrentUser user, CancellationToken ct = default);
}

/// <summary>
/// Broneerimise reeglid:
/// - broneerimiseks on vaja kehtivat kuukaarti, millel on külastusi alles;
/// - broneering võtab kuukaardilt 1 külastuse, tühistamine annab selle tagasi;
/// - tühistada saab kuni 2 tundi enne algust (admin alati);
/// - täis treeningu korral läheb klient ootejärjekorda, külastust siis veel ei võeta;
/// - kliendi treeningute algusaegade vahe peab olema vähemalt 3 tundi (sama aja peale ei saa registreeruda).
/// </summary>
public class BookingService : IBookingService
{
    public const int CancelHoursBefore = 2;
    public const int WaitlistClosesMinutesBefore = 60;
    public const int AdminPageSize = 20;
    /// <summary>Mitu tundi peab olema kliendi kahe treeningu algusaegade vahel.</summary>
    public const int MinHoursBetweenBookings = 3;

    private readonly ISessionRepository _sessions;
    private readonly IBookingRepository _bookings;
    private readonly IMonthlyPassRepository _passes;
    private readonly ITrainerRepository _trainers;
    private readonly IUnitOfWork _uow;
    private readonly ISessionNotifier _notifier;
    private readonly IClock _clock;
    private readonly IUserRepository _users;
    private readonly IEmailSender _email;

    public BookingService(ISessionRepository sessions, IBookingRepository bookings, IMonthlyPassRepository passes,
        ITrainerRepository trainers, IUnitOfWork uow, ISessionNotifier notifier, IClock clock, IUserRepository users, IEmailSender email)
    {
        _users = users;
        _email = email;
        _sessions = sessions;
        _bookings = bookings;
        _passes = passes;
        _trainers = trainers;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<BookingDto> BookAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        var (session, pass) = await CheckCanJoinAsync(sessionId, user, ct);
        var booked = await _sessions.CountBookedAsync(sessionId, ct);

        if (booked < session.Capacity)
        {
            var booking = new Booking
            {
                SessionId = sessionId, Session = session, UserId = user.Id,
                MonthlyPassId = pass.Id, Status = BookingStatus.Booked, CreatedAt = _clock.Now
            };
            pass.VisitsUsed++;
            await _bookings.AddAsync(booking, ct);
            await _uow.SaveChangesAsync(ct);
            await _notifier.FreeSpotsChangedAsync(sessionId, session.Capacity - (booked + 1), ct);
            await SendAsync(user.Id, u => EmailTemplates.Booked(u, session), ct);
            return booking.ToDto(null);
        }

        // Kohti pole → ootejärjekorda.
        var entry = await AddToWaitlistAsync(session, user, ct);
        return entry.Booking.ToDto(entry.Position);
    }

    public async Task<WaitlistDto> JoinWaitlistAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        var (session, _) = await CheckCanJoinAsync(sessionId, user, ct);
        if (await _sessions.CountBookedAsync(sessionId, ct) < session.Capacity)
            throw new ConflictException("Treeningul on veel vabu kohti. Broneeri koht otse: POST /api/sessions/{id}/bookings.", "Vabu kohti on");

        var entry = await AddToWaitlistAsync(session, user, ct);
        return new WaitlistDto(entry.Booking.Id, sessionId, entry.Position);
    }

    /// <summary>Ühised kontrollid: treening on olemas ja pole alanud, klient pole veel registreerunud, kuukaart on olemas.</summary>
    private async Task<(Session Session, MonthlyPass Pass)> CheckCanJoinAsync(int sessionId, CurrentUser user, CancellationToken ct)
    {
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);
        if (session.StartsAt <= _clock.Now)
            throw new BusinessRuleException("Treening on juba alanud või toimunud.");

        var existing = await _bookings.GetForUserAsync(sessionId, user.Id, ct);
        if (existing is not null)
            throw existing.Status == BookingStatus.Booked
                ? new ConflictException("Oled sellele treeningule juba registreerunud.", "Juba registreeritud")
                : new ConflictException("Oled selle treeningu ootejärjekorras juba olemas.", "Juba järjekorras");

        await CheckTimeConflictAsync(session, user, ct);

        var pass = await _passes.GetUsableAsync(user.Id, session.StartsAt, ct)
                   ?? throw new BusinessRuleException("Sul ei ole kehtivat kuukaarti või külastused on otsas. Osta kuukaart: POST /api/passes.",
                       "Kuukaart puudub");
        return (session, pass);
    }

    /// <summary>
    /// Kliendil ei tohi olla teist treeningut (broneeritud või ootejärjekorras), mis algab vähem kui 3 tunni kaugusel.
    /// Kontroll on API-s, nii et see kehtib kõigile klientidele (iPhone, Android, Windows, Swagger).
    /// </summary>
    private async Task CheckTimeConflictAsync(Session session, CurrentUser user, CancellationToken ct)
    {
        var mine = await _bookings.GetAllForUserAsync(user.Id, ct) ?? new List<Booking>();
        var conflict = mine.FirstOrDefault(b =>
            b.SessionId != session.Id
            && b.Status is BookingStatus.Booked or BookingStatus.Waitlisted
            && b.Session is not null
            && Math.Abs((b.Session.StartsAt - session.StartsAt).TotalHours) < MinHoursBetweenBookings);
        if (conflict?.Session is null) return;

        var name = conflict.Session.TrainingType?.Name ?? "treening";
        throw new ConflictException(
            $"Sul on juba treening {name} {conflict.Session.StartsAt:dd.MM HH:mm}. " +
            $"Treeningute vahel peab olema vähemalt {MinHoursBetweenBookings} tundi.",
            "Ajaline kattuvus");
    }

    private async Task<(Booking Booking, int Position)> AddToWaitlistAsync(Session session, CurrentUser user, CancellationToken ct)
    {
        // Tund enne algust järjekord suletakse (Worker eemaldab siis kõik ootajad).
        if (session.StartsAt <= _clock.Now.AddMinutes(WaitlistClosesMinutesBefore))
            throw new SessionFullException(session.Id, waitlistClosed: true);

        var entry = new Booking
        {
            SessionId = session.Id, Session = session, UserId = user.Id,
            Status = BookingStatus.Waitlisted, CreatedAt = _clock.Now
        };
        await _bookings.AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);
        var position = await PositionAsync(entry, ct);
        await SendAsync(user.Id, u => EmailTemplates.Waitlisted(u, session, position), ct);
        return (entry, position);
    }

    /// <summary>Saadab kasutajale e-kirja (kui kasutaja on olemas).</summary>
    private async Task SendAsync(int userId, Func<User, (string Subject, string Html)> build, CancellationToken ct)
    {
        var u = await _users.GetByIdAsync(userId, ct);
        if (u is null) return;
        var (subject, html) = build(u);
        await _email.SendAsync(u.Email, u.FullName, subject, html, ct);
    }

    public async Task<List<BookingDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        var result = new List<BookingDto>();
        foreach (var b in await _bookings.GetAllForUserAsync(userId, ct))
            result.Add(b.ToDto(b.Status == BookingStatus.Waitlisted ? await PositionAsync(b, ct) : null));
        return result;
    }

    public async Task CancelAsync(int bookingId, CurrentUser user, CancellationToken ct = default)
    {
        var booking = await _bookings.GetDetailedAsync(bookingId, ct) ?? throw new NotFoundException("Broneering", bookingId);
        if (booking.UserId != user.Id && !user.IsAdmin)
            throw new ForbiddenException("See on teise kliendi broneering.");

        var session = booking.Session!;
        var now = _clock.Now;

        if (booking.Status == BookingStatus.Waitlisted)
        {
            // Järjekorrast lahkumine: ajapiirangut pole, külastust ei võetud.
            _bookings.Remove(booking);
            await _uow.SaveChangesAsync(ct);
            return;
        }

        if (!user.IsAdmin && session.StartsAt - now < TimeSpan.FromHours(CancelHoursBefore))
            throw new BusinessRuleException($"Tühistada saab kuni {CancelHoursBefore} tundi enne treeningu algust. Külastus jääb kasutatuks.");

        if (session.StartsAt > now && booking.MonthlyPass is { VisitsUsed: > 0 })
            booking.MonthlyPass.VisitsUsed--; // külastus läheb kuukaardile tagasi

        _bookings.Remove(booking);
        await _uow.SaveChangesAsync(ct);

        // Vabanenud koha annab järjekorras esimesele Worker (POST /api/waitlist/promote).
        var booked = await _sessions.CountBookedAsync(session.Id, ct);
        await _notifier.FreeSpotsChangedAsync(session.Id, session.Capacity - booked, ct);
    }

    public async Task<PagedResult<AdminBookingDto>> SearchAsync(string? search, BookingStatus? status, BookingSort sort, int page,
        CancellationToken ct = default)
    {
        if (page < 1) throw new BusinessRuleException("Lehekülg (page) peab olema vähemalt 1.");
        var result = await _bookings.SearchAsync(search, status, sort, page, AdminPageSize, ct);

        var items = new List<AdminBookingDto>();
        foreach (var b in result.Items)
            items.Add(b.ToAdminDto(b.Status == BookingStatus.Waitlisted ? await PositionAsync(b, ct) : null));
        return new PagedResult<AdminBookingDto>(items, result.Page, result.PageSize, result.Total);
    }

    public async Task<AttendanceItemDto> SetAttendanceAsync(int bookingId, bool attended, CurrentUser user, CancellationToken ct = default)
    {
        var booking = await _bookings.GetDetailedAsync(bookingId, ct) ?? throw new NotFoundException("Broneering", bookingId);
        if (!user.IsAdmin)
        {
            var trainer = await _trainers.GetByUserIdAsync(user.Id, ct);
            if (trainer is null || trainer.Id != booking.Session!.TrainerId)
                throw new ForbiddenException("See on teise treeneri treeningu osaleja.");
        }
        if (booking.Status != BookingStatus.Booked)
            throw new BusinessRuleException("See inimene on ootejärjekorras, mitte osaleja.");
        if (booking.Session!.StartsAt > _clock.Now)
            throw new BusinessRuleException("Treening pole veel alanud. Kohalolu saab märkida alates treeningu algusest.");

        booking.Attended = attended;
        await _uow.SaveChangesAsync(ct);
        return booking.ToAttendanceDto();
    }

    /// <summary>Mitmes on kirje ootejärjekorras (1 = esimene).</summary>
    private async Task<int> PositionAsync(Booking entry, CancellationToken ct) =>
        (await _bookings.GetWaitlistAsync(entry.SessionId, ct)).FindIndex(b => b.Id == entry.Id) + 1;
}
