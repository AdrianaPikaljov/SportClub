using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Timetable: sessions, booking, waitlist, participants.</summary>
[ApiController]
[Route("api/sessions")]
[Authorize]
[Produces("application/json")]
[Tags(ApiTags.Schedule)]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IBookingService _bookings;
    private readonly IWaitlistService _waitlist;

    public SessionsController(ISessionService sessions, IBookingService bookings, IWaitlistService waitlist)
    {
        _sessions = sessions;
        _bookings = bookings;
        _waitlist = waitlist;
    }

    /// <summary>Tunniplaan – treeningud koos vabade kohtadega</summary>
    /// <remarks>
    /// Kõik parameetrid on valikulised. Ajad on UTC-s (Eesti talveaeg = UTC + 2 h).
    ///
    /// - **from / to** – ajavahemik (vaikimisi alates tänasest)
    /// - **type** – treeningutüübi id (1 Jooga, 2 HIIT, 3 Spinning, 4 Pilates, 5 Crossfit)
    /// - **trainerId** – treeneri id
    /// - **search** – otsi tüübi, treeneri või ruumi järgi
    /// - **sort** – startsAt (vaikimisi), type, trainer, room; miinus ees = vastupidi (-startsAt)
    /// - **page / pageSize** – leheküljed
    ///
    /// Igal treeningul on **freeSpots** (vabu kohti) ja **waitlistCount** (ootel).
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<SessionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SessionDto>>> GetAll([FromQuery] SessionQuery query, CancellationToken ct) =>
        Ok(await _sessions.SearchAsync(query, ct));

    /// <summary>Üks treening</summary>
    /// <response code="404">Treeningut ei leitud.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionDto>> Get(int id, CancellationToken ct) => Ok(await _sessions.GetAsync(id, ct));

    /// <summary>Broneeri koht treeningule</summary>
    /// <remarks>
    /// Kasutab ühe külastuse kuukaardilt. Kõigile ühendatud klientidele läheb WebSocketi kaudu uus vabade kohtade arv.
    ///
    /// Proovi: Crossfit treeningutel (type=5) on ainult 3 kohta – pärast kolme broneeringut tuleb **409**.
    /// </remarks>
    /// <response code="201">Broneeritud.</response>
    /// <response code="400">Kuukaarti pole või treening on juba alanud.</response>
    /// <response code="404">Treeningut ei leitud.</response>
    /// <response code="409">Treening on täis või sul on juba broneering.</response>
    [HttpPost("{id:int}/bookings")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Booking)]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Book(int id, CancellationToken ct)
    {
        var booking = await _bookings.BookAsync(id, User.ToCurrentUser(), ct);
        return Created($"/api/bookings/{booking.Id}", booking);
    }

    /// <summary>Pane end täis treeningu ootejärjekorda</summary>
    /// <remarks>Töötab ainult siis, kui treening on täis. Vastuses on sinu koht järjekorras (position).</remarks>
    /// <response code="201">Lisatud järjekorda.</response>
    /// <response code="409">Treeningul on veel vabu kohti või oled juba järjekorras.</response>
    [HttpPost("{id:int}/waitlist")]
    [Authorize(Roles = Roles.Client)]
    [Tags(ApiTags.Waitlist)]
    [ProducesResponseType<WaitlistEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WaitlistEntryDto>> JoinWaitlist(int id, CancellationToken ct)
    {
        var entry = await _waitlist.JoinAsync(id, User.ToCurrentUser(), ct);
        return Created($"/api/waitlist/{entry.Id}", entry);
    }

    /// <summary>Minu treeningud (treener)</summary>
    /// <remarks>Treeneri enda treeningud. Samad filtrid nagu tunniplaanil.</remarks>
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Trainer)]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<PagedResult<SessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SessionDto>>> Mine([FromQuery] SessionQuery query, CancellationToken ct) =>
        Ok(await _sessions.GetForTrainerUserAsync(User.ToCurrentUser().Id, query, ct));

    /// <summary>Treeningu osalejad</summary>
    /// <remarks>Treener näeb ainult oma treeningute osalejaid. Vastuses on bookingId, mida on vaja kohalolu märkimiseks.</remarks>
    /// <response code="403">See pole sinu treening.</response>
    [HttpGet("{id:int}/participants")]
    [Authorize(Roles = $"{Roles.Trainer},{Roles.Admin}")]
    [Tags(ApiTags.Trainer)]
    [ProducesResponseType<List<ParticipantDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ParticipantDto>>> Participants(int id, CancellationToken ct) =>
        Ok(await _sessions.GetParticipantsAsync(id, User.ToCurrentUser(), ct));

    /// <summary>Tunniplaan – lisa treening</summary>
    /// <remarks>startsAt on UTC-s: 07:00 Eesti aja järgi = 05:00Z.</remarks>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionDto>> Create(SessionSaveDto dto, CancellationToken ct)
    {
        var created = await _sessions.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Tunniplaan – muuda treeningut</summary>
    /// <remarks>Kohtade arvu ei saa teha väiksemaks kui juba broneeritud (409).</remarks>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionDto>> Update(int id, SessionSaveDto dto, CancellationToken ct) =>
        Ok(await _sessions.UpdateAsync(id, dto, ct));

    /// <summary>Tunniplaan – kustuta treening</summary>
    /// <remarks>Ainult siis, kui aktiivseid broneeringuid pole (muidu 409).</remarks>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _sessions.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Tunni aja jooksul algavad treeningud</summary>
    /// <remarks>Worker kutsub seda iga 15 minuti järel ja saadab nendele treeningutele meeldetuletuse.</remarks>
    [HttpGet("starting")]
    [Authorize(Roles = $"{Roles.Service},{Roles.Admin}")]
    [Tags(ApiTags.Worker)]
    [ProducesResponseType<List<StartingSessionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<StartingSessionDto>>> Starting([FromQuery] int within = 60, CancellationToken ct = default) =>
        Ok(await _sessions.GetStartingAsync(within, ct));
}
