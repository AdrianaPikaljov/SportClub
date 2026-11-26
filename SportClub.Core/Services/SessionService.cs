using System.Globalization;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface ISessionService
{
    /// <summary>Tunniplaan. from/to kujul "2026-11-27", type = tüübi nimi.</summary>
    Task<List<SessionDto>> GetScheduleAsync(string? from, string? to, string? type, CancellationToken ct = default);
    Task<List<SessionDto>> GetForTrainerAsync(int trainerId, string? from, string? to, CancellationToken ct = default);
    Task<SessionSaveDto> GetForEditAsync(int id, CancellationToken ct = default);
    Task<SessionDto> CreateAsync(SessionSaveDto dto, CancellationToken ct = default);
    Task<SessionDto> UpdateAsync(int id, SessionSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<List<TrainerSessionDto>> GetMyTrainerSessionsAsync(CurrentUser user, string? from, string? to, CancellationToken ct = default);
    Task<List<AttendanceItemDto>> GetAttendanceAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    Task<List<AttendanceItemDto>> SaveAttendanceAsync(int sessionId, List<AttendanceItemDto> items, CurrentUser user, CancellationToken ct = default);

    Task<List<StartingSessionDto>> GetStartingAsync(int withinMinutes, CancellationToken ct = default);
}

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessions;
    private readonly ITrainerRepository _trainers;
    private readonly ITrainingTypeRepository _types;
    private readonly IBookingRepository _bookings;
    private readonly IUnitOfWork _uow;
    private readonly ISessionNotifier _notifier;
    private readonly IClock _clock;
    private readonly IEmailSender _email;

    public SessionService(ISessionRepository sessions, ITrainerRepository trainers, ITrainingTypeRepository types,
        IBookingRepository bookings, IUnitOfWork uow, ISessionNotifier notifier, IClock clock, IEmailSender email)
    {
        _email = email;
        _sessions = sessions;
        _trainers = trainers;
        _types = types;
        _bookings = bookings;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
    }

    // ---------- Tunniplaan (kõik) ----------

    public async Task<List<SessionDto>> GetScheduleAsync(string? from, string? to, string? type, CancellationToken ct = default)
    {
        var (fromDate, toDate) = ParseRange(from, to);
        int? typeId = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            var t = await _types.GetByNameAsync(type.Trim(), ct)
                    ?? throw new BusinessRuleException($"Treeningutüüpi \"{type.Trim()}\" ei ole. Vaata GET /api/types.");
            typeId = t.Id;
        }
        return await ListAsync(fromDate, toDate, typeId, null, ct);
    }

    public async Task<List<SessionDto>> GetForTrainerAsync(int trainerId, string? from, string? to, CancellationToken ct = default)
    {
        if (await _trainers.GetByIdAsync(trainerId, ct) is null) throw new NotFoundException("Treener", trainerId);
        var (fromDate, toDate) = ParseRange(from, to);
        return await ListAsync(fromDate, toDate, null, trainerId, ct);
    }

    private async Task<List<SessionDto>> ListAsync(DateTime? from, DateTime? to, int? typeId, int? trainerId, CancellationToken ct)
    {
        var sessions = await _sessions.GetListAsync(from, to, typeId, trainerId, false, ct);
        var counts = await _sessions.GetCountsAsync(sessions.Select(s => s.Id), ct);
        return sessions.Select(s => s.ToDto(counts.TryGetValue(s.Id, out var c) ? c.Booked : 0)).ToList();
    }

    /// <summary>from kaasa arvatud, to päev kaasa arvatud (sellepärast +1 päev).</summary>
    private static (DateTime? From, DateTime? To) ParseRange(string? from, string? to)
    {
        var f = ClubTime.ParseDate(from, "from");
        var t = ClubTime.ParseDate(to, "to");
        if (f is not null && t is not null && f > t)
            throw new BusinessRuleException("Kuupäev 'from' ei saa olla hilisem kui 'to'.");
        return (f, t?.AddDays(1));
    }

    // ---------- Admin: lisa, muuda, kustuta ----------

    public async Task<SessionSaveDto> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var s = await _sessions.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        return new SessionSaveDto
        {
            TypeId = s.TrainingTypeId,
            TrainerId = s.TrainerId,
            Date = ClubTime.FormatDate(s.StartsAt),
            Time = s.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture),
            DurationMinutes = s.DurationMinutes,
            Capacity = s.Capacity
        };
    }

    public async Task<SessionDto> CreateAsync(SessionSaveDto dto, CancellationToken ct = default)
    {
        var start = await ValidateAsync(dto, null, ct);
        var session = new Session
        {
            TrainingTypeId = dto.TypeId,
            TrainerId = dto.TrainerId,
            StartsAt = start,
            DurationMinutes = dto.DurationMinutes,
            Capacity = dto.Capacity
        };
        await _sessions.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);
        return (await _sessions.GetDetailedAsync(session.Id, ct))!.ToDto(0);
    }

    public async Task<SessionDto> UpdateAsync(int id, SessionSaveDto dto, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        var start = await ValidateAsync(dto, id, ct);

        var booked = await _sessions.CountBookedAsync(id, ct);
        if (dto.Capacity < booked)
            throw new ConflictException($"Treeningule on juba registreerunud {booked} inimest, kohtade arv ei saa olla väiksem.");

        session.TrainingTypeId = dto.TypeId;
        session.TrainerId = dto.TrainerId;
        session.StartsAt = start;
        session.DurationMinutes = dto.DurationMinutes;
        session.Capacity = dto.Capacity;
        await _uow.SaveChangesAsync(ct);

        // Kui kohti tuli juurde, viib Worker järjekorrast inimesed uutele kohtadele.
        await _notifier.FreeSpotsChangedAsync(id, session.Capacity - booked, ct);
        return (await _sessions.GetDetailedAsync(id, ct))!.ToDto(booked);
    }

    /// <summary>Kustutab treeningu. Kõik broneeringud tühistatakse ja külastused lähevad kuukaardile tagasi.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var session = await _sessions.GetDetailedAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        var started = session.StartsAt <= _clock.Now;

        var affected = new List<User>();
        foreach (var b in await _bookings.GetForSessionAsync(id, ct))
        {
            if (b.Status == BookingStatus.Booked && !started && b.MonthlyPass is { VisitsUsed: > 0 })
                b.MonthlyPass.VisitsUsed--;
            if (b.User is not null) affected.Add(b.User);
            _bookings.Remove(b);
        }
        _sessions.Remove(session);
        await _uow.SaveChangesAsync(ct);
        await _notifier.FreeSpotsChangedAsync(id, 0, ct);

        // Tulevase treeningu tühistamisest teatame kõigile, kes olid kirjas või järjekorras.
        if (!started)
            foreach (var u in affected)
            {
                var (subject, html) = EmailTemplates.SessionCancelled(u, session);
                await _email.SendAsync(u.Email, u.FullName, subject, html, ct);
            }
    }

    /// <summary>Kontrollib andmeid ja tagastab algusaja.</summary>
    private async Task<DateTime> ValidateAsync(SessionSaveDto dto, int? sessionId, CancellationToken ct)
    {
        if (!DateTime.TryParseExact($"{dto.Date} {dto.Time}", ClubTime.DateTimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var start))
            throw new BusinessRuleException("Vigane kuupäev või kellaaeg. Näide: date \"2026-11-27\", time \"18:00\".");
        if (start < _clock.Now)
            throw new BusinessRuleException("Treening ei saa alata minevikus.");

        if (await _types.GetByIdAsync(dto.TypeId, ct) is null) throw new NotFoundException("Treeningutüüp", dto.TypeId);
        if (await _trainers.GetByIdAsync(dto.TrainerId, ct) is null) throw new NotFoundException("Treener", dto.TrainerId);

        if (await _sessions.TrainerBusyAsync(dto.TrainerId, start, start.AddMinutes(dto.DurationMinutes), sessionId, ct))
            throw new ConflictException("Treeneril on sel ajal juba teine treening.");
        return start;
    }

    // ---------- Treener ----------

    public async Task<List<TrainerSessionDto>> GetMyTrainerSessionsAsync(CurrentUser user, string? from, string? to, CancellationToken ct = default)
    {
        var trainer = await GetTrainerAsync(user, ct);
        var (fromDate, toDate) = ParseRange(from, to);
        var now = _clock.Now;

        var sessions = await _sessions.GetListAsync(fromDate, toDate, null, trainer.Id, newestFirst: true, ct);
        var counts = await _sessions.GetCountsAsync(sessions.Select(s => s.Id), ct);
        return sessions.Select(s =>
        {
            var c = counts.TryGetValue(s.Id, out var x) ? x : new SessionCounts(0, 0, 0);
            var status = s.StartsAt > now ? "Tulemas" : c.Unmarked > 0 ? "Kohalolu märkimata" : "Kohalolu märgitud";
            return new TrainerSessionDto(s.Id, s.TrainingType?.Name ?? "", ClubTime.Format(s.StartsAt), s.DurationMinutes,
                s.Capacity, c.Booked, c.Waitlisted, c.Unmarked, status);
        }).ToList();
    }

    public async Task<List<AttendanceItemDto>> GetAttendanceAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        await GetOwnSessionAsync(sessionId, user, ct);
        return await ParticipantsAsync(sessionId, ct);
    }

    /// <summary>Märgib kohalolu korraga kõigile nimekirjas olijatele.</summary>
    public async Task<List<AttendanceItemDto>> SaveAttendanceAsync(int sessionId, List<AttendanceItemDto> items, CurrentUser user,
        CancellationToken ct = default)
    {
        var session = await GetOwnSessionAsync(sessionId, user, ct);
        if (session.StartsAt > _clock.Now)
            throw new BusinessRuleException("Treening pole veel alanud. Kohalolu saab märkida alates treeningu algusest.");
        if (items.Count == 0)
            throw new BusinessRuleException("Nimekiri on tühi.");

        var booked = (await _bookings.GetForSessionAsync(sessionId, ct))
            .Where(b => b.Status == BookingStatus.Booked)
            .ToDictionary(b => b.Id);

        foreach (var item in items)
        {
            if (!booked.TryGetValue(item.BookingId, out var booking))
                throw new BusinessRuleException($"Broneeringut {item.BookingId} sellel treeningul ei ole.");
            if (item.Attended is null)
                throw new BusinessRuleException($"Broneeringu {item.BookingId} attended peab olema true või false.");
            booking.Attended = item.Attended;
        }
        await _uow.SaveChangesAsync(ct);
        return await ParticipantsAsync(sessionId, ct);
    }

    private async Task<List<AttendanceItemDto>> ParticipantsAsync(int sessionId, CancellationToken ct) =>
        (await _bookings.GetForSessionAsync(sessionId, ct))
            .Where(b => b.Status == BookingStatus.Booked)
            .OrderBy(b => b.User?.FullName)
            .Select(b => b.ToAttendanceDto())
            .ToList();

    /// <summary>Treener näeb ainult oma treeninguid, admin kõiki.</summary>
    private async Task<Session> GetOwnSessionAsync(int sessionId, CurrentUser user, CancellationToken ct)
    {
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);
        if (!user.IsAdmin)
        {
            var trainer = await GetTrainerAsync(user, ct);
            if (trainer.Id != session.TrainerId)
                throw new ForbiddenException("See on teise treeneri treening.");
        }
        return session;
    }

    private async Task<Trainer> GetTrainerAsync(CurrentUser user, CancellationToken ct) =>
        await _trainers.GetByUserIdAsync(user.Id, ct)
        ?? throw new ForbiddenException("Sinu kontoga pole seotud ühtegi treenerit.");

    // ---------- Taustateenus ----------

    public async Task<List<StartingSessionDto>> GetStartingAsync(int withinMinutes, CancellationToken ct = default)
    {
        if (withinMinutes is < 1 or > 1440)
            throw new BusinessRuleException("Parameeter 'within' peab olema 1–1440 minutit.");

        var now = _clock.Now;
        var result = new List<StartingSessionDto>();
        foreach (var s in await _sessions.GetStartingBetweenAsync(now, now.AddMinutes(withinMinutes), ct))
        {
            var pending = await _bookings.GetWithoutReminderAsync(s.Id, ct);
            if (pending.Count == 0) continue;
            result.Add(new StartingSessionDto(s.Id, s.TrainingType?.Name ?? "", ClubTime.Format(s.StartsAt),
                pending.Select(b => new ReminderParticipantDto(b.Id, b.User?.FullName ?? "", b.User?.Email ?? "")).ToList()));
        }
        return result;
    }
}
