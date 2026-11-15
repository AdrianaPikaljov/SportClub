using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Trainers. Everyone logged in can read, only Admin can change.</summary>
[ApiController]
[Route("api/trainers")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Admin)]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _service;

    public TrainersController(ITrainerService service) => _service = service;

    /// <summary>Treenerid – nimekiri</summary>
    /// <remarks>Otsing (search), sorteerimine (sort=name, -name, id) ja leheküljed (page, pageSize).</remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TrainerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TrainerDto>>> GetAll([FromQuery] ListQuery query, CancellationToken ct) =>
        Ok(await _service.SearchAsync(query, ct));

    /// <summary>Treener – üks</summary>
    /// <response code="404">Treenerit ei leitud.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TrainerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerDto>> Get(int id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    /// <summary>Treener – lisa</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrainerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrainerDto>> Create(TrainerSaveDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Treener – muuda</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrainerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerDto>> Update(int id, TrainerSaveDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    /// <summary>Treener – kustuta</summary>
    /// <remarks>Ei saa kustutada, kui treeneril on tunniplaanis treeninguid (409).</remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Training types (Jooga, HIIT, ...).</summary>
[ApiController]
[Route("api/training-types")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Admin)]
public class TrainingTypesController : ControllerBase
{
    private readonly ITrainingTypeService _service;

    public TrainingTypesController(ITrainingTypeService service) => _service = service;

    /// <summary>Treeningutüübid – nimekiri (Jooga, HIIT, ...)</summary>
    /// <remarks>Sorteerimine: name, intensity, id. Seda kasutab ka mobiilirakenduse filter.</remarks>
    [HttpGet]
    [AllowAnonymous]
    [Tags(ApiTags.Schedule)]
    [ProducesResponseType<PagedResult<TrainingTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TrainingTypeDto>>> GetAll([FromQuery] ListQuery query, CancellationToken ct) =>
        Ok(await _service.SearchAsync(query, ct));

    /// <summary>Treeningutüüp – üks</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [Tags(ApiTags.Schedule)]
    [ProducesResponseType<TrainingTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingTypeDto>> Get(int id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    /// <summary>Treeningutüüp – lisa</summary>
    /// <response code="409">Sama nimega tüüp on juba olemas.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrainingTypeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainingTypeDto>> Create(TrainingTypeSaveDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Treeningutüüp – muuda</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<TrainingTypeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TrainingTypeDto>> Update(int id, TrainingTypeSaveDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    /// <summary>Treeningutüüp – kustuta</summary>
    /// <remarks>Ei saa kustutada, kui selle tüübiga on treeninguid (409).</remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
