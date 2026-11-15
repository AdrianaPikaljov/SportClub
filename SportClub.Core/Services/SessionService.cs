using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface ISessionService
{
    Task<PagedResult<SessionDto>> SearchAsync(SessionQuery query, CancellationToken ct = default);
    Task<SessionDto> GetAsync(int id, CancellationToken ct = default);
    Task<SessionDto> CreateAsync(SessionSaveDto dto, CancellationToken ct = default);
    Task<SessionDto> UpdateAsync(int id, SessionSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<List<ParticipantDto>> GetParticipantsAsync(int sessionId, CurrentUser user, CancellationToken ct = default);
    Task<PagedResult<SessionDto>> GetForTrainerUserAsync(int userId, SessionQuery query, CancellationToken ct = default);
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

    public SessionService(ISessionRepository sessions, ITrainerRepository trainers, ITrainingTypeRepository types,
        IBookingRepository bookings, IUnitOfWork uow, ISessionNotifier notifier, IClock clock)
    {
        _sessions = sessions;
        _trainers = trainers;
        _types = types;
        _bookings = bookings;
        _uow = uow;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<PagedResult<SessionDto>> SearchAsync(SessionQuery query, CancellationToken ct = default)
    {
        if (query.From is not null && query.To is not null && query.To < query.From)
            throw new BusinessRuleException("Parameeter 'to' peab olema hilisem kui 'from'.");

        var page = await _sessions.SearchAsync(query, ct);
        var counts = await _sessions.GetCountsAsync(page.Items.Select(s => s.Id), ct);
        return page.Map(s =>
        {
            var (booked, waiting) = counts.TryGetValue(s.Id, out var c) ? c : (0, 0);
            return s.ToDto(booked, waiting);
        });
    }

    public async Task<SessionDto> GetAsync(int id, CancellationToken ct = default)
    {
        var s = await _sessions.GetDetailedAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        return s.ToDto(await _sessions.CountActiveBookingsAsync(id, ct), await _sessions.CountWaitlistAsync(id, ct));
    }

    public async Task<SessionDto> CreateAsync(SessionSaveDto dto, CancellationToken ct = default)
    {
        await EnsureReferencesAsync(dto, ct);
        var session = new Session
        {
            TrainingTypeId = dto.TrainingTypeId,
            TrainerId = dto.TrainerId,
            StartsAt = dto.StartsAt.ToUniversalTime(),
            DurationMinutes = dto.DurationMinutes,
            Room = dto.Room.Trim(),
            MaxParticipants = dto.MaxParticipants
        };
        await _sessions.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetAsync(session.Id, ct);
    }

    public async Task<SessionDto> UpdateAsync(int id, SessionSaveDto dto, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        await EnsureReferencesAsync(dto, ct);

        var booked = await _sessions.CountActiveBookingsAsync(id, ct);
        if (dto.MaxParticipants < booked)
            throw new ConflictException($"Treeningule on juba {booked} broneeringut, kohtade arv ei saa olla väiksem.");

        session.TrainingTypeId = dto.TrainingTypeId;
        session.TrainerId = dto.TrainerId;
        session.StartsAt = dto.StartsAt.ToUniversalTime();
        session.DurationMinutes = dto.DurationMinutes;
        session.Room = dto.Room.Trim();
        session.MaxParticipants = dto.MaxParticipants;
        await _uow.SaveChangesAsync(ct);

        await _notifier.FreeSpotsChangedAsync(id, Math.Max(0, session.MaxParticipants - booked), ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treening", id);
        if (await _sessions.CountActiveBookingsAsync(id, ct) > 0)
            throw new ConflictException("Treeningul on aktiivseid broneeringuid. Tühista need enne kustutamist.");
        _sessions.Remove(session);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<ParticipantDto>> GetParticipantsAsync(int sessionId, CurrentUser user, CancellationToken ct = default)
    {
        var session = await _sessions.GetDetailedAsync(sessionId, ct) ?? throw new NotFoundException("Treening", sessionId);
        if (!user.IsAdmin)
        {
            var trainer = await _trainers.GetByUserIdAsync(user.Id, ct);
            if (trainer is null || trainer.Id != session.TrainerId)
                throw new ForbiddenException("Saad vaadata ainult oma treeningute osalejaid.");
        }

        var bookings = await _bookings.GetActiveForSessionAsync(sessionId, ct);
        return bookings
            .OrderBy(b => b.User!.FullName)
            .Select(b => new ParticipantDto(b.Id, b.UserId, b.User!.FullName, b.User.Email, b.Attended))
            .ToList();
    }

    public async Task<PagedResult<SessionDto>> GetForTrainerUserAsync(int userId, SessionQuery query, CancellationToken ct = default)
    {
        var trainer = await _trainers.GetByUserIdAsync(userId, ct)
                      ?? throw new BusinessRuleException("Sinu kasutajaga pole seotud ühtegi treenerit.");
        query.TrainerId = trainer.Id;
        return await SearchAsync(query, ct);
    }

    public async Task<List<StartingSessionDto>> GetStartingAsync(int withinMinutes, CancellationToken ct = default)
    {
        if (withinMinutes is < 1 or > 24 * 60)
            throw new BusinessRuleException("Parameeter 'within' peab olema vahemikus 1–1440 minutit.");

        var now = _clock.UtcNow;
        var sessions = await _sessions.GetStartingBetweenAsync(now, now.AddMinutes(withinMinutes), ct);
        var result = new List<StartingSessionDto>();
        foreach (var s in sessions)
        {
            var pending = await _bookings.GetActiveWithoutReminderAsync(s.Id, ct);
            result.Add(new StartingSessionDto(s.Id, s.TrainingType?.Name ?? "", DateTime.SpecifyKind(s.StartsAt, DateTimeKind.Utc), pending.Count));
        }
        return result;
    }

    private async Task EnsureReferencesAsync(SessionSaveDto dto, CancellationToken ct)
    {
        if (await _types.GetByIdAsync(dto.TrainingTypeId, ct) is null)
            throw new BusinessRuleException($"Treeningutüüpi {dto.TrainingTypeId} ei ole olemas.");
        if (await _trainers.GetByIdAsync(dto.TrainerId, ct) is null)
            throw new BusinessRuleException($"Treenerit {dto.TrainerId} ei ole olemas.");
    }
}
