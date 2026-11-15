using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Bookings of the logged-in user, cancelling and attendance.</summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Booking)]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _service;

    public BookingsController(IBookingService service) => _service = service;

    /// <summary>Minu broneeringud</summary>
    /// <remarks>upcoming=true (vaikimisi) – ainult tulevased; upcoming=false – kogu ajalugu koos tühistatutega.</remarks>
    [HttpGet("my")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<List<BookingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingDto>>> My([FromQuery] bool upcoming = true, CancellationToken ct = default) =>
        Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, upcoming, ct));

    /// <summary>Tühista broneering</summary>
    /// <remarks>Külastus läheb kuukaardile tagasi. Vabanenud koha saab järjekorras esimene (Worker teeb seda minuti jooksul).</remarks>
    /// <response code="204">Tühistatud.</response>
    /// <response code="400">Treening on juba alanud.</response>
    /// <response code="403">See pole sinu broneering.</response>
    /// <response code="404">Broneeringut ei leitud.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _service.CancelAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }

    /// <summary>Märgi kohalolu</summary>
    /// <remarks>
    /// id = osaleja bookingId (vt GET /api/sessions/{id}/participants). attended: true = kohal, false = puudus.
    /// Saab märkida alates 30 minutit enne treeningu algust.
    /// </remarks>
    /// <response code="403">See pole sinu treening.</response>
    [HttpPatch("{id:int}/attendance")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BookingDto>> Attendance(int id, AttendanceDto dto, CancellationToken ct) =>
        Ok(await _service.SetAttendanceAsync(id, dto.Attended!.Value, User.ToCurrentUser(), ct));
}

/// <summary>Waitlist: my places, leaving, and promotion (Worker).</summary>
[ApiController]
[Route("api/waitlist")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Waitlist)]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _service;

    public WaitlistController(IWaitlistService service) => _service = service;

    /// <summary>Minu kohad ootejärjekorras</summary>
    /// <remarks>position = mitmes oled järjekorras.</remarks>
    [HttpGet("my")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<List<WaitlistEntryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<WaitlistEntryDto>>> My(CancellationToken ct) =>
        Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Lahku ootejärjekorrast</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(int id, CancellationToken ct)
    {
        await _service.LeaveAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }

    /// <summary>Vii järjekorras esimene vabanenud kohale</summary>
    /// <remarks>
    /// Worker kutsub seda iga minuti järel. Kui treeningul on vaba koht ja järjekorras on inimesi,
    /// saab esimene (kes tuli kõige varem) broneeringu ja teavituse. Kehtiva kuukaardita inimene jäetakse vahele.
    /// </remarks>
    [HttpPost("promote")]
    [Authorize(Roles = $"{Roles.Service},{Roles.Admin}")]
    [Tags(ApiTags.Worker)]
    [ProducesResponseType<PromoteResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PromoteResultDto>> Promote(CancellationToken ct) => Ok(await _service.PromoteAsync(ct));
}

/// <summary>Monthly passes with a limited number of visits.</summary>
[ApiController]
[Route("api/passes")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Passes)]
public class PassesController : ControllerBase
{
    private readonly IMonthlyPassService _service;

    public PassesController(IMonthlyPassService service) => _service = service;

    /// <summary>Minu kuukaardid</summary>
    /// <remarks>visitsLeft = külastusi alles, isActive = kehtib praegu, isUpcoming = ostetud, aga hakkab kehtima hiljem.</remarks>
    [HttpGet("my")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<List<MonthlyPassDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MonthlyPassDto>>> My(CancellationToken ct) =>
        Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Osta kuukaart</summary>
    /// <remarks>
    /// planId võta otspunktist GET /api/pass-plans. **Makse on simuleeritud** – päris raha ei võeta.
    /// Kui praegusel kaardil on külastusi alles, hakkab uus kehtima pärast vana lõppu.
    /// </remarks>
    /// <response code="201">Kuukaart ostetud.</response>
    /// <response code="400">Seda paketti enam ei müüda.</response>
    /// <response code="409">Järgmine kuukaart on juba ootel.</response>
    [HttpPost("purchase")]
    [Authorize(Roles = Roles.Client)]
    [ProducesResponseType<MonthlyPassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MonthlyPassDto>> Purchase(PurchasePassRequest request, CancellationToken ct)
    {
        var pass = await _service.PurchaseAsync(request.PlanId, User.ToCurrentUser(), ct);
        return Created($"/api/passes/{pass.Id}", pass);
    }

    /// <summary>Kuukaardid – kõik (otsing kliendi järgi)</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PagedResult<MonthlyPassDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MonthlyPassDto>>> GetAll([FromQuery] ListQuery query, CancellationToken ct) =>
        Ok(await _service.SearchAsync(query, ct));

    /// <summary>Kuukaardid – anna kliendile kaart käsitsi</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<MonthlyPassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MonthlyPassDto>> Create(MonthlyPassCreateDto dto, CancellationToken ct)
    {
        var pass = await _service.CreateAsync(dto, ct);
        return Created($"/api/passes/{pass.Id}", pass);
    }

    /// <summary>Kuukaardid – kustuta kasutamata kaart</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Pass plans the club sells.</summary>
[ApiController]
[Route("api/pass-plans")]
[Produces("application/json")]
[Tags(ApiTags.Passes)]
public class PassPlansController : ControllerBase
{
    private readonly IPassPlanService _service;

    public PassPlansController(IPassPlanService service) => _service = service;

    /// <summary>Kuukaardi paketid (mida saab osta)</summary>
    /// <remarks>Sorteerimine: price (vaikimisi), name, visits. Admin näeb includeInactive=true korral ka müügist eemaldatud pakette.</remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<PassPlanDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PassPlanDto>>> GetAll([FromQuery] ListQuery query, [FromQuery] bool includeInactive = false,
        CancellationToken ct = default) =>
        Ok(await _service.SearchAsync(query, onlyActive: !(includeInactive && User.IsInRole(Roles.Admin)), ct));

    /// <summary>Üks pakett</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<PassPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PassPlanDto>> Get(int id, CancellationToken ct) => Ok(await _service.GetAsync(id, ct));

    /// <summary>Paketid – lisa</summary>
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

    /// <summary>Paketid – muuda</summary>
    /// <remarks>Juba ostetud kuukaardid ei muutu.</remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PassPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PassPlanDto>> Update(int id, PassPlanSaveDto dto, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, dto, ct));

    /// <summary>Paketid – kustuta</summary>
    /// <remarks>Ostetud paketti ei saa kustutada (409) – märgi see mitteaktiivseks (isActive=false).</remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Reminders and notifications.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Worker)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    /// <summary>Saada treeningu meeldetuletus</summary>
    /// <remarks>Worker saadab selle tund enne treeningut kõigile broneerijatele. Teist korda samale inimesele ei saadeta (sent = 0).</remarks>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Service},{Roles.Admin}")]
    [ProducesResponseType<SendReminderResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendReminderResultDto>> Send(SendReminderRequest request, CancellationToken ct) =>
        Ok(await _service.SendRemindersAsync(request.SessionId, ct));

    /// <summary>Minu teavitused</summary>
    /// <remarks>Meeldetuletused, ootejärjekorrast koha saamine, kuukaardi ost.</remarks>
    [HttpGet("my")]
    [Tags(ApiTags.Booking)]
    [ProducesResponseType<List<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NotificationDto>>> My(CancellationToken ct) =>
        Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, ct));
}

/// <summary>Statistics.</summary>
[ApiController]
[Route("api/stats")]
[Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
[Produces("application/json")]
[Tags(ApiTags.Trainer)]
public class StatsController : ControllerBase
{
    private readonly IStatsService _service;

    public StatsController(IStatsService service) => _service = service;

    /// <summary>Populaarseimad treeningud</summary>
    /// <remarks>Treeningutüübid broneeringute arvu järgi. from/to – ajavahemik (valikuline), top – mitu tulemust.</remarks>
    [HttpGet("popular")]
    [ProducesResponseType<List<PopularTrainingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PopularTrainingDto>>> Popular([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int top = 5, CancellationToken ct = default) =>
        Ok(await _service.GetPopularAsync(from, to, top, ct));
}
