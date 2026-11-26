using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Treenerid. Nimekirja näevad kõik, muuta saab ainult admin.</summary>
[ApiController]
[Route("api/trainers")]
[Produces("application/json")]
public class TrainersController : ControllerBase
{
    private readonly ITrainerService _trainers;
    private readonly ISessionService _sessions;

    public TrainersController(ITrainerService trainers, ISessionService sessions)
    {
        _trainers = trainers;
        _sessions = sessions;
    }

    /// <summary>Kõik treenerid koos eriala, kirjelduse ja telefoniga</summary>
    /// <remarks>Lihtsalt vajuta Execute. Admin näeb lisaks treeneri konto e-posti (email).</remarks>
    [HttpGet]
    [AllowAnonymous]
    [Tags(ApiTags.Guest)]
    [ProducesResponseType<List<TrainerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainerDto>>> GetAll(CancellationToken ct) =>
        Ok(await _trainers.GetAllAsync(includeEmail: User.IsInRole(Roles.Admin), ct));

    /// <summary>Treeneri treeningud</summary>
    /// <remarks>
    /// id võta nimekirjast GET /api/trainers. **from / to** on valikulised, kujul AAAA-KK-PP (nt 2026-11-27).
    /// </remarks>
    /// <response code="400">Vigane kuupäev või from on hilisem kui to.</response>
    /// <response code="404">Treenerit ei leitud.</response>
    [HttpGet("{id:int}/sessions")]
    [AllowAnonymous]
    [Tags(ApiTags.Guest)]
    [ProducesResponseType<List<SessionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SessionDto>>> Sessions(int id, [FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        Ok(await _sessions.GetForTrainerAsync(id, from, to, ct));

    /// <summary>Treener muutmiseks (koos konto e-postiga)</summary>
    /// <remarks>
    /// Vastus on samal kujul, mida vajab PUT. Kopeeri see PUT-i, muuda vajalikku ja vajuta Execute.
    /// Parooli vastuses ei ole – uue parooli saab PUT-is anda väljal newPassword.
    /// </remarks>
    /// <response code="404">Treenerit ei leitud.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainerSaveDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainerSaveDto>> Get(int id, CancellationToken ct) => Ok(await _trainers.GetForEditAsync(id, ct));

    /// <summary>Lisa treener (koos sisselogimise kontoga)</summary>
    /// <remarks>Luuakse treener ja tema konto rolliga Trainer. Treener logib sisse sama e-posti ja parooliga.</remarks>
    /// <response code="201">Treener lisatud.</response>
    /// <response code="400">Vigased andmed.</response>
    /// <response code="409">E-post on juba kasutusel.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainerDto>> Create(TrainerCreateDto dto, CancellationToken ct)
    {
        var created = await _trainers.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Muuda treenerit ja tema kontot</summary>
    /// <remarks>
    /// Võta praegused andmed GET /api/trainers/{id} abil. Saab muuta nime, eriala, kirjeldust, telefoni ja konto e-posti.
    /// **newPassword** on valikuline: jäta tühjaks, kui parool jääb samaks.
    /// </remarks>
    /// <response code="400">Vigased andmed.</response>
    /// <response code="404">Treenerit ei leitud.</response>
    /// <response code="409">E-post on teisel kasutajal juba kasutusel.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainerDto>> Update(int id, TrainerUpdateDto dto, CancellationToken ct) =>
        Ok(await _trainers.UpdateAsync(id, dto, ct));

    /// <summary>Kustuta treener</summary>
    /// <remarks>
    /// Kui treeneril on tulevasi treeninguid, tuleb **409** – anna need enne teisele treenerile (PUT /api/sessions/{id}) või kustuta.
    /// Toimunud treeningud ja treeneri konto kustutatakse koos treeneriga.
    /// </remarks>
    /// <response code="204">Kustutatud.</response>
    /// <response code="404">Treenerit ei leitud.</response>
    /// <response code="409">Treeneril on tulevasi treeninguid.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _trainers.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Treeningutüübid (Jooga, Spinning, ...).</summary>
[ApiController]
[Route("api/types")]
[Produces("application/json")]
public class TypesController : ControllerBase
{
    private readonly ITrainingTypeService _service;

    public TypesController(ITrainingTypeService service) => _service = service;

    /// <summary>Kõik treeningutüübid koos kirjeldusega</summary>
    /// <remarks>Lihtsalt vajuta Execute.</remarks>
    [HttpGet]
    [AllowAnonymous]
    [Tags(ApiTags.Guest)]
    [ProducesResponseType<List<TrainingTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TrainingTypeDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetAllAsync(ct));

    /// <summary>Treeningutüüp muutmiseks</summary>
    /// <remarks>Vastus on samal kujul, mida vajab PUT.</remarks>
    /// <response code="404">Tüüpi ei leitud.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainingTypeSaveDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrainingTypeSaveDto>> Get(int id, CancellationToken ct) => Ok(await _service.GetForEditAsync(id, ct));

    /// <summary>Lisa treeningutüüp</summary>
    /// <response code="201">Lisatud.</response>
    /// <response code="400">Vigased andmed.</response>
    /// <response code="409">Sama nimega tüüp on juba olemas.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainingTypeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainingTypeDto>> Create(TrainingTypeSaveDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Muuda treeningutüüpi</summary>
    /// <response code="404">Tüüpi ei leitud.</response>
    /// <response code="409">Sama nimega tüüp on juba olemas.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<TrainingTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TrainingTypeDto>> Update(int id, TrainingTypeSaveDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    /// <summary>Kustuta treeningutüüp</summary>
    /// <remarks>Kui tüüpi kasutatakse tunniplaanis, tuleb **409**.</remarks>
    /// <response code="204">Kustutatud.</response>
    /// <response code="404">Tüüpi ei leitud.</response>
    /// <response code="409">Tüüpi kasutatakse tunniplaanis.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Kuukaardi tariifid, mida klubi müüb.</summary>
[ApiController]
[Route("api/pass-plans")]
[Produces("application/json")]
public class PassPlansController : ControllerBase
{
    private readonly IPassPlanService _service;

    public PassPlansController(IPassPlanService service) => _service = service;

    /// <summary>Kõik tariifid, mida saab osta</summary>
    /// <remarks>Lihtsalt vajuta Execute. Ostmiseks kasuta tariifi id-d: POST /api/passes.</remarks>
    [HttpGet]
    [AllowAnonymous]
    [Tags(ApiTags.Guest)]
    [ProducesResponseType<List<PassPlanDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PassPlanDto>>> GetAll(CancellationToken ct) => Ok(await _service.GetActiveAsync(ct));

    /// <summary>Tariif muutmiseks</summary>
    /// <remarks>Vastus on samal kujul, mida vajab PUT.</remarks>
    /// <response code="404">Tariifi ei leitud.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PassPlanSaveDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PassPlanSaveDto>> Get(int id, CancellationToken ct) => Ok(await _service.GetForEditAsync(id, ct));

    /// <summary>Lisa tariif</summary>
    /// <response code="201">Lisatud.</response>
    /// <response code="400">Vigased andmed.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PassPlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PassPlanDto>> Create(PassPlanSaveDto dto, CancellationToken ct)
    {
        var created = await _service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Muuda tariifi</summary>
    /// <remarks>Juba ostetud kuukaardid ei muutu.</remarks>
    /// <response code="400">Vigased andmed.</response>
    /// <response code="404">Tariifi ei leitud.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PassPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PassPlanDto>> Update(int id, PassPlanSaveDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    /// <summary>Eemalda tariif müügist</summary>
    /// <remarks>Juba ostetud kuukaarte see ei puuduta, tariif lihtsalt kaob müügist.</remarks>
    /// <response code="204">Eemaldatud.</response>
    /// <response code="404">Tariifi ei leitud.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
