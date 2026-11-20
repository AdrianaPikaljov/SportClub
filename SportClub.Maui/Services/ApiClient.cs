using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SportClub.Maui.Models;

namespace SportClub.Maui.Services;

/// <summary>Error returned by the API as ProblemDetails.</summary>
public class ApiException : Exception
{
    public ApiException(int status, string message) : base(message) => Status = status;
    public int Status { get; }
}

/// <summary>All calls to the Web API. The app never touches the database directly.</summary>
public class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly AuthState _auth;

    public ApiClient(AuthState auth)
    {
        _auth = auth;
        _http = new HttpClient { BaseAddress = new Uri(ApiConfig.BaseUrl), Timeout = TimeSpan.FromSeconds(20) };
    }

    // ----- auth -----
    public Task<AuthResponse> LoginAsync(string email, string password) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", new { email, password }, auth: false);

    public Task<AuthResponse> RegisterAsync(string fullName, string email, string password) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", new { fullName, email, password }, auth: false);

    // ----- client -----
    public Task<PagedResult<PassPlanDto>> GetPassPlansAsync() =>
        SendAsync<PagedResult<PassPlanDto>>(HttpMethod.Get, "api/pass-plans?pageSize=50", auth: false);

    public Task<MonthlyPassDto> PurchasePassAsync(int planId) =>
        SendAsync<MonthlyPassDto>(HttpMethod.Post, "api/passes/purchase", new { planId });

    public Task<PagedResult<SessionDto>> GetSessionsAsync(DateTime fromUtc, DateTime toUtc, int? typeId) =>
        SendAsync<PagedResult<SessionDto>>(HttpMethod.Get,
            $"api/sessions?from={Uri.EscapeDataString(fromUtc.ToString("O"))}&to={Uri.EscapeDataString(toUtc.ToString("O"))}" +
            $"{(typeId is null ? "" : $"&type={typeId}")}&pageSize=100");

    public Task<SessionDto> GetSessionAsync(int id) => SendAsync<SessionDto>(HttpMethod.Get, $"api/sessions/{id}");

    public Task<PagedResult<TrainingTypeDto>> GetTrainingTypesAsync() =>
        SendAsync<PagedResult<TrainingTypeDto>>(HttpMethod.Get, "api/training-types?pageSize=100", auth: false);

    public Task<BookingDto> BookAsync(int sessionId) => SendAsync<BookingDto>(HttpMethod.Post, $"api/sessions/{sessionId}/bookings");

    public Task<WaitlistEntryDto> JoinWaitlistAsync(int sessionId) =>
        SendAsync<WaitlistEntryDto>(HttpMethod.Post, $"api/sessions/{sessionId}/waitlist");

    public Task<List<BookingDto>> GetMyBookingsAsync(bool upcoming) =>
        SendAsync<List<BookingDto>>(HttpMethod.Get, $"api/bookings/my?upcoming={upcoming.ToString().ToLowerInvariant()}");

    public Task CancelBookingAsync(int bookingId) => SendAsync<object>(HttpMethod.Delete, $"api/bookings/{bookingId}");

    public Task<List<WaitlistEntryDto>> GetMyWaitlistAsync() => SendAsync<List<WaitlistEntryDto>>(HttpMethod.Get, "api/waitlist/my");

    public Task LeaveWaitlistAsync(int entryId) => SendAsync<object>(HttpMethod.Delete, $"api/waitlist/{entryId}");

    public Task<List<MonthlyPassDto>> GetMyPassesAsync() => SendAsync<List<MonthlyPassDto>>(HttpMethod.Get, "api/passes/my");

    public Task<List<NotificationDto>> GetMyNotificationsAsync() => SendAsync<List<NotificationDto>>(HttpMethod.Get, "api/notifications/my");

    // ----- trainer -----
    public Task<PagedResult<SessionDto>> GetMyTrainerSessionsAsync() =>
        SendAsync<PagedResult<SessionDto>>(HttpMethod.Get,
            $"api/sessions/mine?from={Uri.EscapeDataString(DateTime.UtcNow.AddHours(-3).ToString("O"))}&pageSize=50");

    public Task<List<ParticipantDto>> GetParticipantsAsync(int sessionId) =>
        SendAsync<List<ParticipantDto>>(HttpMethod.Get, $"api/sessions/{sessionId}/participants");

    public Task SetAttendanceAsync(int bookingId, bool attended) =>
        SendAsync<object>(HttpMethod.Patch, $"api/bookings/{bookingId}/attendance", new { attended });

    public Task<List<PopularTrainingDto>> GetPopularAsync(DateTime? fromUtc) =>
        SendAsync<List<PopularTrainingDto>>(HttpMethod.Get,
            $"api/stats/popular?top=10{(fromUtc is null ? "" : $"&from={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}")}");

    // ----- plumbing -----
    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null, bool auth = true)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        if (auth && _auth.Token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(0, $"Serveriga ei saa ühendust ({ApiConfig.BaseUrl}). Kas API töötab?");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent || typeof(T) == typeof(object))
                    return default!;
                return (await response.Content.ReadFromJsonAsync<T>(Json))!;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && auth)
                _auth.SignOut();

            ProblemDetailsDto? problem = null;
            try { problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(Json); } catch { /* not json */ }
            var message = problem?.Detail
                          ?? (problem?.Errors is { Count: > 0 } ? string.Join(" ", problem.Errors.Values.SelectMany(v => v)) : null)
                          ?? problem?.Title
                          ?? $"Viga {(int)response.StatusCode}";
            throw new ApiException((int)response.StatusCode, message);
        }
    }
}
