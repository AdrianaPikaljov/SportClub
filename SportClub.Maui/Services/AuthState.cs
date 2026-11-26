using SportClub.Maui.Models;

namespace SportClub.Maui.Services;

public static class ApiConfig
{
    private const string UrlKey = "api_url";

    /// <summary>Telefonis (iPhone, Android) saab serveri aadressi sisselogimise lehel muuta.</summary>
    public static bool IsPhone => DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android;

    /// <summary>
    /// Windowsis on API samas arvutis (localhost). Android emulaator näeb arvuti localhost-i kui 10.0.2.2.
    /// Päris telefonis tuleb sisestada arvuti IP-aadress (start.bat näitab seda), nt http://192.168.1.15:5080.
    /// </summary>
    public static string DefaultUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080/" : "http://localhost:5080/";

    public static string BaseUrl
    {
        get
        {
            var saved = IsPhone ? Preferences.Default.Get(UrlKey, "") : "";
            return string.IsNullOrWhiteSpace(saved) ? DefaultUrl : saved;
        }
    }

    /// <summary>Salvestab aadressi. Lubab ka lühikuju "192.168.1.15" – lisame http:// ja pordi 5080.</summary>
    public static void SetBaseUrl(string? text)
    {
        var url = (text ?? "").Trim();
        if (url.Length == 0)
        {
            Preferences.Default.Remove(UrlKey);
            return;
        }
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "http://" + url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort && !url.Contains(":" + uri.Port) ? 5080 : uri.Port;
            url = $"{uri.Scheme}://{uri.Host}:{port}/";
        }
        Preferences.Default.Set(UrlKey, url);
    }

    public static string WebSocketUrl => BaseUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "ws/sessions";
}

/// <summary>Hoiab JWT-d ja sisseloginud kasutajat. Token on SecureStorage-is.</summary>
public class AuthState
{
    private const string TokenKey = "jwt";
    private const string UserKey = "user";

    public string? Token { get; private set; }
    public SignedInUser? User { get; private set; }

    public bool IsLoggedIn => Token is not null && User is not null;
    public bool IsTrainer => User?.Role == "Trainer";

    public event EventHandler? SignedOut;

    public async Task SignInAsync(LoginResponse response, string email)
    {
        Token = response.Token;
        User = new SignedInUser(response.Name, email, response.Role);
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, response.Token);
            Preferences.Default.Set(UserKey, System.Text.Json.JsonSerializer.Serialize(User, ApiClient.Json));
        }
        catch
        {
            // SecureStorage may be unavailable on some emulators – the session still works in memory.
        }
    }

    public async Task<bool> TryRestoreAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            var userJson = Preferences.Default.Get<string?>(UserKey, null);
            if (token is null || userJson is null) return false;
            Token = token;
            User = System.Text.Json.JsonSerializer.Deserialize<SignedInUser>(userJson, ApiClient.Json);
            return User?.Role is not null && User.Name is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SignOut()
    {
        var wasLoggedIn = Token is not null;
        Token = null;
        User = null;
        SecureStorage.Default.Remove(TokenKey);
        Preferences.Default.Remove(UserKey);
        if (wasLoggedIn) SignedOut?.Invoke(this, EventArgs.Empty);
    }
}
