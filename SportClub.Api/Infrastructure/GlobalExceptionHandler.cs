using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SportClub.Core.Exceptions;

namespace SportClub.Api.Infrastructure;

/// <summary>
/// One place that turns every exception into a ProblemDetails response.
/// Controllers do not need try/catch.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Title, app.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Autentimine ebaõnnestus", exception.Message),
            OperationCanceledException => (499, "Päring katkestati", "Klient katkestas päringu."),
            _ => (StatusCodes.Status500InternalServerError, "Serveri viga", "Midagi läks valesti. Proovi hiljem uuesti.")
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("{Status} {Title}: {Detail} ({Method} {Path})", status, title, detail, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.io/{status}",
                Instance = context.Request.Path
            }
        });
    }
}
