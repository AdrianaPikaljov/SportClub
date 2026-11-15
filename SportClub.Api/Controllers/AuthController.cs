using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportClub.Api.Infrastructure;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Services;

namespace SportClub.Api.Controllers;

/// <summary>Login, registration and user info.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[Tags(ApiTags.Auth)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Logi sisse – vastuses on token</summary>
    /// <remarks>
    /// Saada e-post ja parool. Vastuses tuleb **token**, mida kasutad nupu **Authorize** all.
    /// Token kehtib 8 tundi ja selles on kirjas kasutaja roll.
    ///
    /// Testkontod: klient@sportclub.ee / Klient123! · treener@sportclub.ee / Treener123! · admin@sportclub.ee / Admin123!
    /// </remarks>
    /// <response code="200">Sisselogimine õnnestus.</response>
    /// <response code="400">E-post või parool on tühi.</response>
    /// <response code="401">Vale e-post või parool.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    /// <summary>Registreeri uus klient</summary>
    /// <remarks>Loob kliendi konto (roll Client) ja logib kohe sisse. Kuukaarti uuel kliendil veel pole – selle saab osta.</remarks>
    /// <response code="201">Konto loodud, vastuses on token.</response>
    /// <response code="400">Vigased andmed (nt parool alla 8 tähemärgi).</response>
    /// <response code="409">Selle e-postiga konto on juba olemas.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(Me), null, result);
    }

    /// <summary>Kes ma olen? (sisseloginud kasutaja andmed)</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) =>
        Ok(await _auth.GetUserAsync(User.ToCurrentUser().Id, ct));

    /// <summary>Kasutajate nimekiri</summary>
    /// <remarks>Filtreeri rolli järgi (role=Client), otsi nime või e-posti järgi (search=mari), lehekülgedena (page, pageSize).</remarks>
    [HttpGet("/api/users")]
    [Authorize(Roles = Roles.Admin)]
    [Tags(ApiTags.Admin)]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<UserDto>>> Users([FromQuery] ListQuery query, [FromQuery] string? role, CancellationToken ct) =>
        Ok(await _auth.SearchUsersAsync(query, role, ct));
}
