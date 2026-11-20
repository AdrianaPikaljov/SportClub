using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SportClub.Maui.Models;

namespace SportClub.Maui.Services;

/// <summary>
/// Keeps a WebSocket connection to /ws/sessions and raises FreeSpotsChanged
/// whenever the server reports a new number of free spots. Reconnects automatically.
/// </summary>
public class RealtimeService
{
    private readonly AuthState _auth;
    private CancellationTokenSource? _cts;

    public RealtimeService(AuthState auth) => _auth = auth;

    public event EventHandler<FreeSpotsChangedMessage>? FreeSpotsChanged;

    public bool IsConnected { get; private set; }
    public event EventHandler<bool>? ConnectionChanged;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        SetConnected(false);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_auth.Token is null) break;
            using var socket = new ClientWebSocket();
            try
            {
                var uri = new Uri($"{ApiConfig.WebSocketUrl}?access_token={Uri.EscapeDataString(_auth.Token)}");
                await socket.ConnectAsync(uri, ct);
                SetConnected(true);
                await ReceiveLoopAsync(socket, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebSocket error: {ex.Message}");
            }

            SetConnected(false);
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) return;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            var message = JsonSerializer.Deserialize<FreeSpotsChangedMessage>(json, ApiClient.Json);
            if (message?.Type == "FreeSpotsChanged")
                MainThread.BeginInvokeOnMainThread(() => FreeSpotsChanged?.Invoke(this, message));
        }
    }

    private void SetConnected(bool value)
    {
        if (IsConnected == value) return;
        IsConnected = value;
        MainThread.BeginInvokeOnMainThread(() => ConnectionChanged?.Invoke(this, value));
    }
}
