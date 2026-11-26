using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Broneeringud: minu broneeringud, tühistamine, admini nimekiri.</summary>
[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _service;

    public BookingsController(IBookingService service) => _service = service;

    /// <summary>Minu broneeringud ja järjekorrakohad</summary>
    /// <remarks>
    /// Lihtsalt vajuta Execute.
    /// - status **Booked** – koht on olemas
    /// - status **Waitlisted** – ootejärjekorras, **position** = mitmes oled
    /// - attended: null = märkimata, true = olid kohal, false = ei tulnud
    /// </remarks>
    [HttpGet("my")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Client)]
    [ProducesResponseType<List<BookingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingDto>>> My(CancellationToken ct) =>
        Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Tühista broneering või lahku järjekorrast</summary>
    /// <remarks>
    /// id võta "minu broneeringutest".
    /// - **Booked**: tühistada saab kuni 2 tundi enne algust. Külastus läheb kuukaardile tagasi ja koht vabaneb –
    ///   Worker viib selle kohale järjekorras esimese.
    /// - **Waitlisted**: lahkud järjekorrast, ajapiirangut pole, teised liiguvad ette.
    ///
    /// Admin saab tühistada iga broneeringu ilma 2 tunni piiranguta.
    /// </remarks>
    /// <response code="204">Tühistatud.</response>
    /// <response code="400">Algus on vähem kui 2 tunni pärast.</response>
    /// <response code="403">See on teise kliendi broneering.</response>
    /// <response code="404">Broneeringut ei leitud.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    [Tags(ApiTags.Client)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        await _service.CancelAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }

    /// <summary>Märgi ühe inimese kohalolu (ülesande nõue)</summary>
    /// <remarks>
    /// Sama mis PATCH /api/sessions/{id}/attendance, aga ühele inimesele. id = bookingId osalejate nimekirjast.
    /// </remarks>
    /// <response code="400">Treening pole alanud või see on järjekorrakoht.</response>
    /// <response code="403">Teise treeneri treeningu osaleja.</response>
    /// <response code="404">Broneeringut ei leitud.</response>
    [HttpPatch("{id:int}/attendance")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<AttendanceItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AttendanceItemDto>> Attendance(int id, AttendanceRequest request, CancellationToken ct) =>
        Ok(await _service.SetAttendanceAsync(id, request.Attended!.Value, User.ToCurrentUser(), ct));

    /// <summary>Kõik broneeringud (otsing, filter, sorteerimine, leheküljed)</summary>
    /// <remarks>
    /// Kõik parameetrid on valikulised:
    /// - **search** – kliendi nimi
    /// - **status** – Booked või Waitlisted
    /// - **sort** – Uusimad (vaikimisi) või Vanimad
    /// - **page** – lehekülg, 20 rida lehel
    /// </remarks>
    /// <response code="400">page peab olema vähemalt 1.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PagedResult<AdminBookingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<AdminBookingDto>>> Search([FromQuery] string? search, [FromQuery] BookingStatus? status,
        [FromQuery] BookingSort sort = BookingSort.Uusimad, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Ok(await _service.SearchAsync(search, status, sort, page, ct));
}

/// <summary>Kuukaardid.</summary>
[ApiController]
[Route("api/passes")]
[Produces("application/json")]
public class PassesController : ControllerBase
{
    private readonly IMonthlyPassService _service;

    public PassesController(IMonthlyPassService service) => _service = service;

    /// <summary>Minu kuukaart</summary>
    /// <remarks>Lihtsalt vajuta Execute. visitsLeft = mitu külastust on alles.</remarks>
    /// <response code="404">Aktiivset kuukaarti pole.</response>
    [HttpGet("my")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Client)]
    [ProducesResponseType<MyPassDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MyPassDto>> My(CancellationToken ct) => Ok(await _service.GetMyAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Osta kuukaart</summary>
    /// <remarks>
    /// Kehas ainult tariifi id (vt GET /api/pass-plans). Kehtib tänasest. Makse on simuleeritud.
    /// Korraga saab olla ainult üks aktiivne kuukaart.
    /// </remarks>
    /// <response code="201">Ostetud.</response>
    /// <response code="404">Sellist tariifi pole.</response>
    /// <response code="409">Aktiivne kuukaart on juba olemas.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Client)]
    [ProducesResponseType<MyPassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MyPassDto>> Buy(BuyPassRequest request, CancellationToken ct)
    {
        var pass = await _service.BuyAsync(request.PlanId, User.ToCurrentUser(), ct);
        return Created("/api/passes/my", pass);
    }

    /// <summary>Kõik ostetud kuukaardid</summary>
    /// <remarks>Lihtsalt vajuta Execute. **search** (valikuline) – kliendi nimi.</remarks>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<List<PassDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PassDto>>> GetAll([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _service.SearchAsync(search, ct));
}

/// <summary>Ootejärjekord (Worker).</summary>
[ApiController]
[Route("api/waitlist")]
[Produces("application/json")]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _service;

    public WaitlistController(IWaitlistService service) => _service = service;

    /// <summary>Vii järjekorras esimesed vabanenud kohtadele</summary>
    /// <remarks>
    /// Keha pole vaja. Worker kutsub seda iga minuti järel, admin võib kutsuda käsitsi (nt kaitsmisel).
    ///
    /// - Treeningutel, mis algavad rohkem kui tunni pärast: kui on vaba koht, saab järjekorras esimene broneeringu
    ///   (Waitlisted → Booked) ja kuukaardilt võetakse 1 külastus.
    /// - Kui esimesel pole kehtivat kuukaarti, eemaldatakse ta järjekorrast ja võetakse järgmine.
    /// - Tund enne algust eemaldatakse kõik, kes on veel järjekorras.
    ///
    /// Vastus on 200 ka siis, kui kedagi polnud viia (tühi nimekiri).
    /// </remarks>
    [HttpPost("promote")]
    [Authorize(Roles = $"{Roles.Service},{Roles.Admin}")]
    [Tags(ApiTags.Worker)]
    [ProducesResponseType<PromoteResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PromoteResultDto>> Promote(CancellationToken ct) => Ok(await _service.PromoteAsync(ct));
}

/// <summary>Meeldetuletused (Worker).</summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service) => _service = service;

    /// <summary>Saada meeldetuletus ühele osalejale</summary>
    /// <remarks>
    /// bookingId võta GET /api/sessions/starting vastusest. Päris e-kirja ei saadeta: meeldetuletus salvestatakse
    /// tabelisse Notifications ja logisse ning broneering märgitakse "meelde tuletatud".
    /// </remarks>
    /// <response code="201">Saadetud.</response>
    /// <response code="400">See on järjekorrakoht, mitte osaleja.</response>
    /// <response code="404">Broneeringut ei leitud.</response>
    /// <response code="409">Sellele broneeringule on juba meelde tuletatud.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Service)]
    [Tags(ApiTags.Worker)]
    [ProducesResponseType<NotificationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NotificationDto>> Send(NotificationRequest request, CancellationToken ct)
    {
        var result = await _service.SendReminderAsync(request.BookingId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

/// <summary>Statistika.</summary>
[ApiController]
[Route("api/stats")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _service;

    public StatsController(IStatsService service) => _service = service;

    /// <summary>Populaarseimad treeningud</summary>
    /// <remarks>
    /// Lihtsalt vajuta Execute. Treeningutüübid alates kõige populaarsemast.
    /// **Treener** näeb ainult oma treeninguid, **admin** kogu klubi.
    ///
    /// fillRate = mitu protsenti kohtadest on keskmiselt täidetud. from / to (AAAA-KK-PP) on valikulised.
    /// </remarks>
    /// <response code="400">Vigane kuupäev.</response>
    [HttpGet("popular")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<List<PopularDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<PopularDto>>> Popular([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        Ok(await _service.GetPopularAsync(from, to, User.ToCurrentUser(), ct));
}
