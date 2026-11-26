using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SportClub.Maui.Models;

namespace SportClub.Maui.Services;

/// <summary>Error returned by the API as ProblemDetails.</summary>
public class ApiException : Exception
{
    public ApiException(int status, string message, int? position = null) : base(message)
    {
        Status = status;
        Position = position;
    }

    public int Status { get; }
    /// <summary>Koht ootejärjekorras (409 vastusest, kui treening oli täis).</summary>
    public int? Position { get; }
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
        // Aadress võetakse igal päringul uuesti, sest telefonis saab seda sisselogimise lehel muuta.
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    // ----- sisselogimine -----
    public Task<LoginResponse> LoginAsync(string email, string password) =>
        SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { email, password }, auth: false);

    public Task<UserDto> RegisterAsync(string name, string email, string password) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register", new { name, email, password }, auth: false);

    // ----- tunniplaan (külaline) -----
    /// <summary>Treeningud päevade kaupa: from ja to on kaasa arvatud. type = tüübi nimi.</summary>
    public Task<List<SessionDto>> GetSessionsAsync(DateTime from, DateTime to, string? type) =>
        SendAsync<List<SessionDto>>(HttpMethod.Get,
            $"api/sessions?from={ApiTime.Date(from)}&to={ApiTime.Date(to)}{(type is null ? "" : $"&type={Uri.EscapeDataString(type)}")}",
            auth: false);

    public Task<List<TrainingTypeDto>> GetTrainingTypesAsync() =>
        SendAsync<List<TrainingTypeDto>>(HttpMethod.Get, "api/types", auth: false);

    public Task<List<PassPlanDto>> GetPassPlansAsync() =>
        SendAsync<List<PassPlanDto>>(HttpMethod.Get, "api/pass-plans", auth: false);

    // ----- klient -----
    /// <summary>Kehtiv kuukaart või null, kui seda pole (API vastab 404).</summary>
    public async Task<MonthlyPassDto?> GetMyPassAsync()
    {
        try
        {
            return await SendAsync<MonthlyPassDto>(HttpMethod.Get, "api/passes/my");
        }
        catch (ApiException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>Kaardi andmed lähevad ainult API-sse (makse on simuleeritud) – rakendus neid ei salvesta.</summary>
    public Task<MonthlyPassDto> BuyPassAsync(int planId, string cardNumber, string cardHolder, string expiry, string cvc) =>
        SendAsync<MonthlyPassDto>(HttpMethod.Post, "api/passes", new { planId, cardNumber, cardHolder, expiry, cvc });

    /// <summary>Registreerumine. Täis treeningul vastab API 409 ja paneb kliendi ootejärjekorda.</summary>
    public async Task<BookResult> BookAsync(int sessionId)
    {
        try
        {
            await SendAsync<BookingDto>(HttpMethod.Post, $"api/sessions/{sessionId}/bookings");
            return new BookResult(false, null);
        }
        catch (ApiException ex) when (ex.Status == 409 && ex.Position is not null)
        {
            return new BookResult(true, ex.Position);
        }
    }

    public Task<List<BookingDto>> GetMyBookingsAsync() => SendAsync<List<BookingDto>>(HttpMethod.Get, "api/bookings/my");

    /// <summary>Tühistab broneeringu või eemaldab ootejärjekorrast.</summary>
    public Task CancelBookingAsync(int bookingId) => SendAsync<object>(HttpMethod.Delete, $"api/bookings/{bookingId}");

    // ----- treener -----
    public Task<List<TrainerSessionDto>> GetMyTrainerSessionsAsync() =>
        SendAsync<List<TrainerSessionDto>>(HttpMethod.Get, "api/trainer/sessions");

    public Task<List<AttendanceItemDto>> GetAttendanceAsync(int sessionId) =>
        SendAsync<List<AttendanceItemDto>>(HttpMethod.Get, $"api/sessions/{sessionId}/attendance");

    /// <summary>Märgib mitu osalejat korraga (PATCH /api/sessions/{id}/attendance).</summary>
    public Task<List<AttendanceItemDto>> SaveAttendanceAsync(int sessionId, IEnumerable<AttendanceItemDto> items) =>
        SendAsync<List<AttendanceItemDto>>(HttpMethod.Patch, $"api/sessions/{sessionId}/attendance", items.ToList());

    public Task SetAttendanceAsync(int bookingId, bool attended) =>
        SendAsync<object>(HttpMethod.Patch, $"api/bookings/{bookingId}/attendance", new { attended });

    public Task<List<PopularDto>> GetPopularAsync(DateTime? from) =>
        SendAsync<List<PopularDto>>(HttpMethod.Get, $"api/stats/popular{(from is null ? "" : $"?from={ApiTime.Date(from.Value)}")}");

    // ----- plumbing -----
    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null, bool auth = true)
    {
        using var request = new HttpRequestMessage(method, new Uri(new Uri(ApiConfig.BaseUrl), url));
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
            throw new ApiException(0, Loc.T("error.connection", ApiConfig.BaseUrl));
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
            throw new ApiException((int)response.StatusCode, message, problem?.Position);
        }
    }
}
