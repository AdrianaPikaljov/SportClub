using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Tunniplaan, broneerimine, osalejad ja kohalolu.</summary>
[ApiController]
[Route("api/sessions")]
[Produces("application/json")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IBookingService _bookings;

    public SessionsController(ISessionService sessions, IBookingService bookings)
    {
        _sessions = sessions;
        _bookings = bookings;
    }

    // ---------- Külaline ----------

    /// <summary>Tunniplaan – kõik treeningud koos vabade kohtadega</summary>
    /// <remarks>
    /// Ilma parameetriteta näitab kõiki treeninguid aja järgi, ka neid, kus kohti enam pole (**free = 0**).
    ///
    /// Kõik parameetrid on valikulised:
    /// - **from** – alates päevast, kujul AAAA-KK-PP, nt 2026-11-27
    /// - **to** – kuni päevani (kaasa arvatud), nt 2026-11-30
    /// - **type** – treeningutüüp, vali nimekirjast
    ///
    /// Võib anda ka ainult ühe kuupäeva: from=2026-12-01 näitab kõike alates 1. detsembrist.
    /// </remarks>
    /// <param name="from">Alates päevast (AAAA-KK-PP)</param>
    /// <param name="to">Kuni päevani kaasa arvatud (AAAA-KK-PP)</param>
    /// <param name="type">Treeningutüübi nimi</param>
    /// <param name="ct"></param>
    /// <response code="400">Kuupäev on vale kujuga või from on hilisem kui to.</response>
    [HttpGet]
    [AllowAnonymous]
    [Tags(ApiTags.Guest)]
    [ProducesResponseType<List<SessionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SessionDto>>> GetAll([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? type,
        CancellationToken ct) =>
        Ok(await _sessions.GetScheduleAsync(from, to, type, ct));

    // ---------- Klient ----------

    /// <summary>Registreeru treeningule</summary>
    /// <remarks>
    /// Sisesta ainult treeningu id tunniplaanist, päringu keha pole vaja.
    ///
    /// - **Kohti on** → 201, oled kirjas ja kuukaardilt võetakse 1 külastus.
    /// - **Kohti pole** → **409**, aga sind pannakse automaatselt ootejärjekorda. Vastuses on sinu koht järjekorras (**position**).
    ///   Kui keegi tühistab, viib Worker sind minuti jooksul treeningule ja võtab siis külastuse.
    ///
    /// Proovi: homme kell 19:00 Crossfit on täis.
    /// </remarks>
    /// <response code="201">Registreeritud.</response>
    /// <response code="400">Treening on möödas, kuukaart puudub või külastused on otsas.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    /// <response code="409">Treening on täis (lisati ootejärjekorda) või oled juba registreerunud / järjekorras.</response>
    [HttpPost("{id:int}/bookings")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Client)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Book(int id, CancellationToken ct)
    {
        var booking = await _bookings.BookAsync(id, User.ToCurrentUser(), ct);
        if (booking.Status == nameof(BookingStatus.Booked))
            return Created($"/api/bookings/{booking.Id}", booking);

        // Täis: ülesanne nõuab 409, aga klient on juba järjekorras – see on näha vastuses.
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Treening on täis",
            Detail = $"Sind lisati ootejärjekorda, sinu koht: {booking.Position}",
            Type = "https://httpstatuses.io/409",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["bookingId"] = booking.Id;
        problem.Extensions["position"] = booking.Position;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict, ContentTypes = { "application/problem+json" } };
    }

    /// <summary>Pane end ootejärjekorda (ülesande nõue)</summary>
    /// <remarks>
    /// Klient seda tavaliselt ei vaja: POST /api/sessions/{id}/bookings paneb täis treeningul ise järjekorda.
    /// Töötab ainult siis, kui treening on täis.
    /// </remarks>
    /// <response code="201">Lisatud järjekorda.</response>
    /// <response code="400">Treening on möödas või kuukaart puudub.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    /// <response code="409">Kohti on veel (broneeri otse) või oled juba registreerunud / järjekorras.</response>
    [HttpPost("{id:int}/waitlist")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Client)]
    [ProducesResponseType<WaitlistDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WaitlistDto>> JoinWaitlist(int id, CancellationToken ct)
    {
        var entry = await _bookings.JoinWaitlistAsync(id, User.ToCurrentUser(), ct);
        return Created($"/api/bookings/{entry.Id}", entry);
    }

    // ---------- Treener ----------

    /// <summary>Osalejate nimekiri</summary>
    /// <remarks>
    /// Sisesta treeningu id (vt GET /api/trainer/sessions). attended: null = märkimata.
    /// Kopeeri vastus PATCH-i, kirjuta null asemele true (oli kohal) või false (ei olnud).
    /// Treener näeb ainult nimesid, mitte klientide e-posti.
    /// </remarks>
    /// <response code="403">See on teise treeneri treening.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    [HttpGet("{id:int}/attendance")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<List<AttendanceItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AttendanceItemDto>>> GetAttendance(int id, CancellationToken ct) =>
        Ok(await _sessions.GetAttendanceAsync(id, User.ToCurrentUser(), ct));

    /// <summary>Märgi kohalolu kõigile korraga</summary>
    /// <remarks>
    /// Kleebi siia GET /api/sessions/{id}/attendance vastus ja kirjuta igale **true** (oli kohal) või **false** (ei olnud).
    /// Väli name on ainult mugavuseks, salvestamisel seda ei kasutata.
    ///
    /// Märkida saab alates treeningu algusest. Hiljem võib uuesti saata, kui tegid vea.
    /// </remarks>
    /// <response code="200">Salvestatud, vastuses on uuendatud nimekiri.</response>
    /// <response code="400">Treening pole alanud, nimekirjas on võõras bookingId või attended pole true/false.</response>
    /// <response code="403">See on teise treeneri treening.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    [HttpPatch("{id:int}/attendance")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<List<AttendanceItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AttendanceItemDto>>> SaveAttendance(int id, List<AttendanceItemDto> items, CancellationToken ct) =>
        Ok(await _sessions.SaveAttendanceAsync(id, items, User.ToCurrentUser(), ct));

    // ---------- Admin ----------

    /// <summary>Treening muutmiseks</summary>
    /// <remarks>Vastus on täpselt samal kujul, mida vajab PUT. Kopeeri see PUT-i, muuda vajalikku ja vajuta Execute.</remarks>
    /// <response code="404">Treeningut ei leitud.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<SessionSaveDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionSaveDto>> Get(int id, CancellationToken ct) => Ok(await _sessions.GetForEditAsync(id, ct));

    /// <summary>Lisa treening tunniplaani</summary>
    /// <remarks>Kuupäev ja kellaaeg on eraldi: date "2026-11-27", time "18:00" (Eesti aeg).</remarks>
    /// <response code="201">Lisatud.</response>
    /// <response code="400">Kuupäev minevikus, vale kuju või kohtade arv alla 1.</response>
    /// <response code="404">Sellist tüüpi või treenerit pole.</response>
    /// <response code="409">Treeneril on sel ajal juba treening.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> Create(SessionSaveDto dto, CancellationToken ct)
    {
        var created = await _sessions.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Muuda treeningut</summary>
    /// <remarks>
    /// Võta praegused andmed GET /api/sessions/{id} abil.
    /// Kohtade arvu ei saa teha väiksemaks kui juba registreerunute arv (**409**).
    /// Kui kohti lisandub, viib Worker järjekorrast inimesed uutele kohtadele.
    /// </remarks>
    /// <response code="400">Vigased andmed.</response>
    /// <response code="404">Treeningut, tüüpi või treenerit ei leitud.</response>
    /// <response code="409">Kohti vähem kui registreerunuid või treeneril on sel ajal teine treening.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> Update(int id, SessionSaveDto dto, CancellationToken ct) =>
        Ok(await _sessions.UpdateAsync(id, dto, ct));

    /// <summary>Kustuta treening</summary>
    /// <remarks>Kõik broneeringud ja järjekorrakohad tühistatakse, klientidele tagastatakse külastused kuukaardile.</remarks>
    /// <response code="204">Kustutatud.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _sessions.DeleteAsync(id, ct);
        return NoContent();
    }

    // ---------- Taustateenus ----------

    /// <summary>Peagi algavad treeningud (meeldetuletuste jaoks)</summary>
    /// <remarks>
    /// Treeningud, mis algavad järgmise **within** minuti jooksul, koos osalejatega, kellele pole veel meelde tuletatud.
    /// Worker kutsub seda iga 15 minuti järel.
    /// </remarks>
    /// <response code="400">within peab olema 1–1440.</response>
    [HttpGet("starting")]
    [Authorize(Roles = Roles.Service)]
    [Tags(ApiTags.Worker)]
    [ProducesResponseType<List<StartingSessionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<StartingSessionDto>>> Starting([FromQuery] int within = 60, CancellationToken ct = default) =>
        Ok(await _sessions.GetStartingAsync(within, ct));
}

/// <summary>Treeneri enda treeningud.</summary>
[ApiController]
[Route("api/trainer")]
[Produces("application/json")]
public class TrainerController : ControllerBase
{
    private readonly ISessionService _sessions;

    public TrainerController(ISessionService sessions) => _sessions = sessions;

    /// <summary>Minu treeningud (toimunud ja tulevased)</summary>
    /// <remarks>
    /// Lihtsalt vajuta Execute: kõik sinu treeningud, uuemad eespool. Nii leiad ka toimunud treeningu id, et kohalolu parandada.
    ///
    /// status:
    /// - **Tulemas** – pole veel alanud, märkida ei saa
    /// - **Kohalolu märkimata** – toimunud, aga kõik pole märgitud (**unmarked** = mitu inimest on märkimata)
    /// - **Kohalolu märgitud** – kõik märgitud (parandada saab ikka)
    ///
    /// from / to (AAAA-KK-PP) on valikuline filter.
    /// </remarks>
    /// <response code="400">Vigane kuupäev.</response>
    [HttpGet("sessions")]
    [Authorize(Roles = Roles.Trainer)]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<List<TrainerSessionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<TrainerSessionDto>>> MySessions([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct) =>
        Ok(await _sessions.GetMyTrainerSessionsAsync(User.ToCurrentUser(), from, to, ct));
}
