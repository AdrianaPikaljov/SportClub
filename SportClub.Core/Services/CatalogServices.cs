using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

// ---------- Treenerid ----------

public interface ITrainerService
{
    /// <summary>includeEmail = true ainult adminile.</summary>
    Task<List<TrainerDto>> GetAllAsync(bool includeEmail, CancellationToken ct = default);
    Task<TrainerSaveDto> GetForEditAsync(int id, CancellationToken ct = default);
    Task<TrainerDto> CreateAsync(TrainerCreateDto dto, CancellationToken ct = default);
    Task<TrainerDto> UpdateAsync(int id, TrainerUpdateDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class TrainerService : ITrainerService
{
    private readonly ITrainerRepository _trainers;
    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly IPasswordService _passwords;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public TrainerService(ITrainerRepository trainers, IUserRepository users, ISessionRepository sessions,
        IPasswordService passwords, IUnitOfWork uow, IClock clock)
    {
        _trainers = trainers;
        _users = users;
        _sessions = sessions;
        _passwords = passwords;
        _uow = uow;
        _clock = clock;
    }

    public async Task<List<TrainerDto>> GetAllAsync(bool includeEmail, CancellationToken ct = default) =>
        (await _trainers.GetAllAsync(ct)).Select(t => t.ToDto(includeEmail)).ToList();

    public async Task<TrainerSaveDto> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var t = await _trainers.GetWithUserAsync(id, ct) ?? throw new NotFoundException("Treener", id);
        return new TrainerSaveDto
        {
            Name = t.FullName, Specialty = t.Specialty, Description = t.Description, Phone = t.Phone, Email = t.User?.Email ?? ""
        };
    }

    public async Task<TrainerDto> CreateAsync(TrainerCreateDto dto, CancellationToken ct = default)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Selle e-postiga kasutaja on juba olemas.");

        // Treeneriga koos luuakse tema sisselogimise konto (roll Trainer).
        var user = new User { FullName = dto.Name.Trim(), Email = email, Role = Roles.Trainer, CreatedAt = _clock.Now };
        user.PasswordHash = _passwords.Hash(user, dto.Password);
        var trainer = new Trainer
        {
            FullName = dto.Name.Trim(),
            Specialty = dto.Specialty.Trim(),
            Description = dto.Description,
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            User = user
        };
        await _users.AddAsync(user, ct);
        await _trainers.AddAsync(trainer, ct);
        await _uow.SaveChangesAsync(ct);
        return trainer.ToDto(includeEmail: true);
    }

    /// <summary>Muudab treeneri andmed ja tema konto (e-post, soovi korral uus parool).</summary>
    public async Task<TrainerDto> UpdateAsync(int id, TrainerUpdateDto dto, CancellationToken ct = default)
    {
        var trainer = await _trainers.GetWithUserAsync(id, ct) ?? throw new NotFoundException("Treener", id);
        var email = dto.Email.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(dto.NewPassword) && dto.NewPassword.Length < 8)
            throw new BusinessRuleException("Uus parool peab olema vähemalt 8 tähemärki.");

        var other = await _users.GetByEmailAsync(email, ct);
        if (other is not null && other.Id != trainer.UserId)
            throw new ConflictException("Selle e-postiga kasutaja on juba olemas.");

        trainer.FullName = dto.Name.Trim();
        trainer.Specialty = dto.Specialty.Trim();
        trainer.Description = dto.Description;
        trainer.Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim();

        if (trainer.User is null)
        {
            // Vanal treeneril polnud kontot – loome selle (siis on parool kohustuslik).
            if (string.IsNullOrWhiteSpace(dto.NewPassword))
                throw new BusinessRuleException("Treeneril pole veel kontot. Konto loomiseks sisesta newPassword.");
            trainer.User = new User { Role = Roles.Trainer, CreatedAt = _clock.Now };
            await _users.AddAsync(trainer.User, ct);
        }

        trainer.User.FullName = trainer.FullName;
        trainer.User.Email = email;
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            trainer.User.PasswordHash = _passwords.Hash(trainer.User, dto.NewPassword);

        await _uow.SaveChangesAsync(ct);
        return trainer.ToDto(includeEmail: true);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var trainer = await _trainers.GetWithUserAsync(id, ct) ?? throw new NotFoundException("Treener", id);
        if (await _trainers.HasSessionsAfterAsync(id, _clock.Now, ct))
            throw new ConflictException("Treeneril on tulevasi treeninguid. Anna need teisele treenerile või kustuta need enne.");

        // Toimunud treeningud kustutatakse koos treeneriga, samuti tema konto.
        foreach (var s in await _sessions.GetAllForTrainerAsync(id, ct))
            _sessions.Remove(s);
        if (trainer.User is not null) _users.Remove(trainer.User);
        _trainers.Remove(trainer);
        await _uow.SaveChangesAsync(ct);
    }
}

// ---------- Treeningutüübid ----------

public interface ITrainingTypeService
{
    Task<List<TrainingTypeDto>> GetAllAsync(CancellationToken ct = default);
    Task<TrainingTypeSaveDto> GetForEditAsync(int id, CancellationToken ct = default);
    Task<TrainingTypeDto> CreateAsync(TrainingTypeSaveDto dto, CancellationToken ct = default);
    Task<TrainingTypeDto> UpdateAsync(int id, TrainingTypeSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class TrainingTypeService : ITrainingTypeService
{
    private readonly ITrainingTypeRepository _types;
    private readonly IUnitOfWork _uow;

    public TrainingTypeService(ITrainingTypeRepository types, IUnitOfWork uow)
    {
        _types = types;
        _uow = uow;
    }

    public async Task<List<TrainingTypeDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _types.GetAllAsync(ct)).Select(t => t.ToDto()).ToList();

    public async Task<TrainingTypeSaveDto> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var t = await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id);
        return new TrainingTypeSaveDto { Name = t.Name, Description = t.Description };
    }

    public async Task<TrainingTypeDto> CreateAsync(TrainingTypeSaveDto dto, CancellationToken ct = default)
    {
        if (await _types.NameExistsAsync(dto.Name.Trim(), null, ct))
            throw new ConflictException($"Treeningutüüp \"{dto.Name.Trim()}\" on juba olemas.");
        var type = new TrainingType { Name = dto.Name.Trim(), Description = dto.Description };
        await _types.AddAsync(type, ct);
        await _uow.SaveChangesAsync(ct);
        return type.ToDto();
    }

    public async Task<TrainingTypeDto> UpdateAsync(int id, TrainingTypeSaveDto dto, CancellationToken ct = default)
    {
        var type = await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id);
        if (await _types.NameExistsAsync(dto.Name.Trim(), id, ct))
            throw new ConflictException($"Treeningutüüp \"{dto.Name.Trim()}\" on juba olemas.");
        type.Name = dto.Name.Trim();
        type.Description = dto.Description;
        await _uow.SaveChangesAsync(ct);
        return type.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var type = await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id);
        if (await _types.HasSessionsAsync(id, ct))
            throw new ConflictException("Seda tüüpi kasutatakse tunniplaanis. Kustuta või muuda need treeningud enne.");
        _types.Remove(type);
        await _uow.SaveChangesAsync(ct);
    }
}

// ---------- Kuukaardi tariifid ----------

public interface IPassPlanService
{
    Task<List<PassPlanDto>> GetActiveAsync(CancellationToken ct = default);
    Task<PassPlanSaveDto> GetForEditAsync(int id, CancellationToken ct = default);
    Task<PassPlanDto> CreateAsync(PassPlanSaveDto dto, CancellationToken ct = default);
    Task<PassPlanDto> UpdateAsync(int id, PassPlanSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class PassPlanService : IPassPlanService
{
    private readonly IPassPlanRepository _plans;
    private readonly IUnitOfWork _uow;

    public PassPlanService(IPassPlanRepository plans, IUnitOfWork uow)
    {
        _plans = plans;
        _uow = uow;
    }

    public async Task<List<PassPlanDto>> GetActiveAsync(CancellationToken ct = default) =>
        (await _plans.GetActiveAsync(ct)).Select(p => p.ToDto()).ToList();

    public async Task<PassPlanSaveDto> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var p = await GetActivePlanAsync(id, ct);
        return new PassPlanSaveDto { Name = p.Name, Visits = p.Visits, Days = p.Days, Price = p.Price };
    }

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
        var plan = await GetActivePlanAsync(id, ct);
        Apply(plan, dto); // juba ostetud kuukaardid ei muutu, neil on oma andmed
        await _uow.SaveChangesAsync(ct);
        return plan.ToDto();
    }

    /// <summary>Tariif eemaldatakse müügist. Juba ostetud kuukaardid jäävad alles.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var plan = await GetActivePlanAsync(id, ct);
        plan.IsActive = false;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<PassPlan> GetActivePlanAsync(int id, CancellationToken ct)
    {
        var plan = await _plans.GetByIdAsync(id, ct);
        if (plan is null || !plan.IsActive) throw new NotFoundException("Tariif", id);
        return plan;
    }

    private static void Apply(PassPlan plan, PassPlanSaveDto dto)
    {
        plan.Name = dto.Name.Trim();
        plan.Visits = dto.Visits;
        plan.Days = dto.Days;
        plan.Price = Math.Round(dto.Price, 2);
    }
}

// ---------- Sisselogimine ja kasutajad ----------

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    string CreateToken(User user);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<List<UserDto>> SearchUsersAsync(string? search, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordService _passwords;
    private readonly ITokenService _tokens;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AuthService(IUserRepository users, IPasswordService passwords, ITokenService tokens, IUnitOfWork uow, IClock clock)
    {
        _users = users;
        _passwords = passwords;
        _tokens = tokens;
        _uow = uow;
        _clock = clock;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        // Ei ütle, kas vale oli e-post või parool.
        if (user is null || !_passwords.Verify(user, request.Password))
            throw new UnauthorizedAccessException("Vale e-post või parool.");
        return new LoginResponse(_tokens.CreateToken(user), user.Role, user.FullName);
    }

    /// <summary>Registreerimine loob alati kliendi. Rolli päringus ei saadeta.</summary>
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Selle e-postiga kasutaja on juba olemas.");

        var user = new User { FullName = request.Name.Trim(), Email = email, Role = Roles.Client, CreatedAt = _clock.Now };
        user.PasswordHash = _passwords.Hash(user, request.Password);
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<List<UserDto>> SearchUsersAsync(string? search, CancellationToken ct = default) =>
        (await _users.SearchAsync(search, ct)).Select(u => u.ToDto()).ToList();
}
