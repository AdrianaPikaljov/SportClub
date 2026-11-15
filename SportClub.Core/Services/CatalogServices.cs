using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

public interface ITrainerService
{
    Task<PagedResult<TrainerDto>> SearchAsync(ListQuery query, CancellationToken ct = default);
    Task<TrainerDto> GetAsync(int id, CancellationToken ct = default);
    Task<TrainerDto> CreateAsync(TrainerSaveDto dto, CancellationToken ct = default);
    Task<TrainerDto> UpdateAsync(int id, TrainerSaveDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class TrainerService : ITrainerService
{
    private readonly ITrainerRepository _trainers;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;

    public TrainerService(ITrainerRepository trainers, IUserRepository users, IUnitOfWork uow)
    {
        _trainers = trainers;
        _users = users;
        _uow = uow;
    }

    public async Task<PagedResult<TrainerDto>> SearchAsync(ListQuery query, CancellationToken ct = default) =>
        (await _trainers.SearchAsync(query, ct)).Map(t => t.ToDto());

    public async Task<TrainerDto> GetAsync(int id, CancellationToken ct = default) =>
        (await _trainers.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treener", id)).ToDto();

    public async Task<TrainerDto> CreateAsync(TrainerSaveDto dto, CancellationToken ct = default)
    {
        await ValidateUserLinkAsync(dto.UserId, null, ct);
        var trainer = new Trainer { FullName = dto.FullName.Trim(), Bio = dto.Bio, Phone = dto.Phone, UserId = dto.UserId };
        await _trainers.AddAsync(trainer, ct);
        await _uow.SaveChangesAsync(ct);
        return trainer.ToDto();
    }

    public async Task<TrainerDto> UpdateAsync(int id, TrainerSaveDto dto, CancellationToken ct = default)
    {
        var trainer = await _trainers.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treener", id);
        await ValidateUserLinkAsync(dto.UserId, id, ct);
        trainer.FullName = dto.FullName.Trim();
        trainer.Bio = dto.Bio;
        trainer.Phone = dto.Phone;
        trainer.UserId = dto.UserId;
        await _uow.SaveChangesAsync(ct);
        return trainer.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var trainer = await _trainers.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treener", id);
        if (await _trainers.HasSessionsAsync(id, ct))
            throw new ConflictException("Treeneril on tunniplaanis treeninguid. Kustuta või muuda need enne.");
        _trainers.Remove(trainer);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task ValidateUserLinkAsync(int? userId, int? trainerId, CancellationToken ct)
    {
        if (userId is null) return;
        var user = await _users.GetByIdAsync(userId.Value, ct) ?? throw new NotFoundException("Kasutaja", userId.Value);
        if (user.Role != Roles.Trainer)
            throw new BusinessRuleException("Seotud kasutaja peab olema rolliga Trainer.");
        var other = await _trainers.GetByUserIdAsync(userId.Value, ct);
        if (other is not null && other.Id != trainerId)
            throw new ConflictException("See kasutaja on juba seotud teise treeneriga.");
    }
}

public interface ITrainingTypeService
{
    Task<PagedResult<TrainingTypeDto>> SearchAsync(ListQuery query, CancellationToken ct = default);
    Task<TrainingTypeDto> GetAsync(int id, CancellationToken ct = default);
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

    public async Task<PagedResult<TrainingTypeDto>> SearchAsync(ListQuery query, CancellationToken ct = default) =>
        (await _types.SearchAsync(query, ct)).Map(t => t.ToDto());

    public async Task<TrainingTypeDto> GetAsync(int id, CancellationToken ct = default) =>
        (await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id)).ToDto();

    public async Task<TrainingTypeDto> CreateAsync(TrainingTypeSaveDto dto, CancellationToken ct = default)
    {
        if (await _types.NameExistsAsync(dto.Name.Trim(), null, ct))
            throw new ConflictException($"Treeningutüüp \"{dto.Name}\" on juba olemas.");
        var type = new TrainingType { Name = dto.Name.Trim(), Description = dto.Description, Intensity = dto.Intensity };
        await _types.AddAsync(type, ct);
        await _uow.SaveChangesAsync(ct);
        return type.ToDto();
    }

    public async Task<TrainingTypeDto> UpdateAsync(int id, TrainingTypeSaveDto dto, CancellationToken ct = default)
    {
        var type = await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id);
        if (await _types.NameExistsAsync(dto.Name.Trim(), id, ct))
            throw new ConflictException($"Treeningutüüp \"{dto.Name}\" on juba olemas.");
        type.Name = dto.Name.Trim();
        type.Description = dto.Description;
        type.Intensity = dto.Intensity;
        await _uow.SaveChangesAsync(ct);
        return type.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var type = await _types.GetByIdAsync(id, ct) ?? throw new NotFoundException("Treeningutüüp", id);
        if (await _types.HasSessionsAsync(id, ct))
            throw new ConflictException("Selle tüübiga on tunniplaanis treeninguid.");
        _types.Remove(type);
        await _uow.SaveChangesAsync(ct);
    }
}
