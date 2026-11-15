using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SportClub.Core.Dtos;
using SportClub.Core.Interfaces;

namespace SportClub.Api.Realtime;

/// <summary>
/// Keeps all open WebSocket connections on /ws/sessions and broadcasts
/// FreeSpotsChanged {sessionId, free} to every connected client.
/// Registered as a singleton; the service layer calls it after saving data.
/// </summary>
public class WebSocketSessionNotifier : ISessionNotifier
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, (WebSocket Socket, SemaphoreSlim Lock)> _sockets = new();
    private readonly ILogger<WebSocketSessionNotifier> _logger;

    public WebSocketSessionNotifier(ILogger<WebSocketSessionNotifier> logger) => _logger = logger;

    public int ConnectionCount => _sockets.Count;

    /// <summary>Runs for the lifetime of one connection.</summary>
    public async Task HandleConnectionAsync(WebSocket socket, string user, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        _sockets[id] = (socket, new SemaphoreSlim(1, 1));
        _logger.LogInformation("WebSocket connected: {User} ({Count} open)", user, _sockets.Count);

        var buffer = new byte[1024];
        try
        {
            // We only push from the server; reading keeps the connection alive and detects close.
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bye", CancellationToken.None);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // client disappeared
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            _logger.LogInformation("WebSocket closed: {User} ({Count} open)", user, _sockets.Count);
        }
    }

    public async Task FreeSpotsChangedAsync(int sessionId, int free, CancellationToken ct = default)
    {
        var message = new FreeSpotsChangedMessage(sessionId, Math.Max(0, free));
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Json));

        foreach (var (id, (socket, gate)) in _sockets)
        {
            if (socket.State != WebSocketState.Open)
            {
                _sockets.TryRemove(id, out _);
                continue;
            }
            await gate.WaitAsync(ct);
            try
            {
                // A WebSocket allows only one send at a time, hence the lock.
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WebSocket send failed, dropping connection");
                _sockets.TryRemove(id, out _);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
