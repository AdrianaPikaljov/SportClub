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
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger, IWebHostEnvironment env)
    {
        _problemDetails = problemDetails;
        _logger = logger;
        _env = env;
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
        {
            _logger.LogError(exception, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
            WriteErrorFile(context, exception);
            // Arenduses näitame põhjust ka kliendile, et viga oleks lihtsam leida.
            if (_env.IsDevelopment())
                detail = $"{detail} ({exception.GetBaseException().Message})";
        }
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

    /// <summary>Serveri vead lähevad ka faili logs/errors.log (API kaustas), et neid saaks hiljem vaadata.</summary>
    private void WriteErrorFile(HttpContext context, Exception exception)
    {
        try
        {
            var dir = Path.Combine(_env.ContentRootPath, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "errors.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context.Request.Method} {context.Request.Path}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // logifaili viga ei tohi vastust katki teha
        }
    }
}
