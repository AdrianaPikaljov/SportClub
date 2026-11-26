using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SportClub.Worker;

public class WorkerOptions
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";
    public string Email { get; set; } = "service@sportclub.ee";
    public string Password { get; set; } = "Service123!";
    public int WaitlistIntervalSeconds { get; set; } = 60;
    public int ReminderIntervalMinutes { get; set; } = 15;
    public int ReminderWithinMinutes { get; set; } = 60;
}

// API vastuste koopiad (Worker ei viita API projektile ega DbContext-ile).
public record LoginResponse(string Token, string Role, string Name);
public record Promoted(int BookingId, int SessionId, string Name);
public record PromoteResult(List<Promoted> Promoted, int Removed);
public record Participant(int BookingId, string Name, string Email);
public record StartingSession(int SessionId, string Type, string StartsAt, List<Participant> Participants);
public record NotificationResult(int Id, int BookingId, string Name, string Message, string CreatedAt);

/// <summary>
/// HttpClient API jaoks. Logib sisse Service kontoga ja logib uuesti sisse, kui token aegub (401).
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly WorkerOptions _options;
    private readonly ILogger<ApiClient> _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private string? _token;

    public ApiClient(HttpClient http, WorkerOptions options, ILogger<ApiClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
        _http.BaseAddress = new Uri(options.ApiBaseUrl);
    }

    public Task<PromoteResult?> PromoteWaitlistAsync(CancellationToken ct) =>
        SendAsync<PromoteResult>(() => new HttpRequestMessage(HttpMethod.Post, "api/waitlist/promote"), ct);

    public Task<List<StartingSession>?> GetStartingSessionsAsync(int withinMinutes, CancellationToken ct) =>
        SendAsync<List<StartingSession>>(() => new HttpRequestMessage(HttpMethod.Get, $"api/sessions/starting?within={withinMinutes}"), ct);

    /// <summary>Saadab meeldetuletuse. null = sellele broneeringule oli juba saadetud (409).</summary>
    public Task<NotificationResult?> SendReminderAsync(int bookingId, CancellationToken ct) =>
        SendAsync<NotificationResult>(() => new HttpRequestMessage(HttpMethod.Post, "api/notifications")
        {
            Content = JsonContent.Create(new { bookingId })
        }, ct, conflictIsOk: true);

    private async Task<T?> SendAsync<T>(Func<HttpRequestMessage> createRequest, CancellationToken ct, bool conflictIsOk = false)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await EnsureTokenAsync(force: attempt == 2, ct);
            using var request = createRequest();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
            {
                _logger.LogInformation("Token rejected, logging in again");
                continue;
            }
            if (response.StatusCode == HttpStatusCode.Conflict && conflictIsOk)
                return default;
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"{request.Method} {request.RequestUri} → {(int)response.StatusCode}: {body}");
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        return default;
    }

    private async Task EnsureTokenAsync(bool force, CancellationToken ct)
    {
        if (!force && _token is not null) return;

        await _loginLock.WaitAsync(ct);
        try
        {
            if (!force && _token is not null) return;
            var response = await _http.PostAsJsonAsync("api/auth/login", new { email = _options.Email, password = _options.Password }, ct);
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct)
                        ?? throw new InvalidOperationException("Empty login response");
            _token = login.Token;
            _logger.LogInformation("Logged in to API as {Email} (Service)", _options.Email);
        }
        finally
        {
            _loginLock.Release();
        }
    }
}
