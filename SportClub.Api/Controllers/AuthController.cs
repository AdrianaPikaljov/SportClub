using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Registreerimine, sisselogimine ja kasutajate nimekiri.</summary>
[ApiController]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Registreeri uus klient</summary>
    /// <remarks>
    /// Loob konto alati rolliga **Client**. Rolli päringus ei saadeta – muidu saaks igaüks endast admini teha.
    /// Vastuses parooli ei ole. Seejärel logi sisse: POST /api/auth/login.
    /// </remarks>
    /// <response code="201">Konto loodud.</response>
    /// <response code="400">Tühjad väljad, vigane e-post või parool alla 8 tähemärgi.</response>
    /// <response code="409">Selle e-postiga konto on juba olemas.</response>
    [HttpPost("api/auth/register")]
    [AllowAnonymous]
    [Tags(ApiTags.Auth)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = await _auth.RegisterAsync(request, ct);
        return Created($"/api/users/{user.Id}", user);
    }

    /// <summary>Logi sisse – vastuses on token</summary>
    /// <remarks>
    /// Kopeeri vastusest **token**, vajuta üleval **Authorize** ja kleebi see sinna.
    ///
    /// Testkontod (vaheta näites e-post ja parool):
    /// - klient@sportclub.ee / Klient123!
    /// - treener@sportclub.ee / Treener123!
    /// - admin@sportclub.ee / Admin123!
    /// - service@sportclub.ee / Service123!
    /// </remarks>
    /// <response code="200">Sisselogimine õnnestus.</response>
    /// <response code="400">E-post või parool on tühi.</response>
    /// <response code="401">Vale e-post või parool.</response>
    [HttpPost("api/auth/login")]
    [AllowAnonymous]
    [Tags(ApiTags.Auth)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    /// <summary>Kõik kasutajad</summary>
    /// <remarks>Lihtsalt vajuta Execute. **search** (valikuline) – otsi nime järgi.</remarks>
    [HttpGet("api/users")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<List<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserDto>>> Users([FromQuery] string? search, CancellationToken ct) =>
        Ok(await _auth.SearchUsersAsync(search, ct));

    /// <summary>Kustuta kasutaja</summary>
    /// <remarks>
    /// Kustutab kasutaja koos tema broneeringute, ootejärjekorra kohtade, kuukaartide ja teavitustega.
    /// Vabanenud kohtade arv saadetakse kohe kõigile (WebSocket), ootejärjekorra järgmise viib kohale Worker.
    ///
    /// Ei saa kustutada: iseennast (400), taustateenuse kontot (409) ja treenerit – treener kustutatakse
    /// koos kontoga DELETE /api/trainers/{id} kaudu (409).
    /// </remarks>
    /// <response code="204">Kustutatud.</response>
    /// <response code="400">Iseennast ei saa kustutada.</response>
    /// <response code="404">Sellist kasutajat pole.</response>
    /// <response code="409">Taustateenuse või treeneri konto.</response>
    [HttpDelete("api/users/{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken ct)
    {
        await _auth.DeleteUserAsync(id, User.ToCurrentUser(), ct);
        return NoContent();
    }
}
