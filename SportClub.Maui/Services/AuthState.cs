using SportClub.Maui.Models;

namespace SportClub.Maui.Services;

public static class ApiConfig
{
    /// <summary>
    /// Android emulator sees the PC's localhost as 10.0.2.2.
    /// On a real phone use the PC's IP address (and run the API with --urls http://0.0.0.0:5080).
    /// </summary>
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080/" : "http://localhost:5080/";

    public static string WebSocketUrl => BaseUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "ws/sessions";
}

/// <summary>Keeps the JWT and logged-in user. Token is stored in SecureStorage.</summary>
public class AuthState
{
    private const string TokenKey = "jwt";
    private const string UserKey = "user";

    public string? Token { get; private set; }
    public UserDto? User { get; private set; }

    public bool IsLoggedIn => Token is not null && User is not null;
    public bool IsTrainer => User?.Role == "Trainer";

    public event EventHandler? SignedOut;

    public async Task SignInAsync(AuthResponse response)
    {
        Token = response.Token;
        User = response.User;
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, response.Token);
            Preferences.Default.Set(UserKey, System.Text.Json.JsonSerializer.Serialize(response.User, ApiClient.Json));
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
            User = System.Text.Json.JsonSerializer.Deserialize<UserDto>(userJson, ApiClient.Json);
            return User is not null;
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
