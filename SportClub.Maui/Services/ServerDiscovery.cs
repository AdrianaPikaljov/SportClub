using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SportClub.Maui.Services;

/// <summary>
/// Leiab telefonist SportClubi API kohtvõrgust ise üles: proovib läbi kõik sama võrgu aadressid
/// (nt 192.168.1.1 … 192.168.1.254) pordil 5080 ja küsib /api/ping. Esimene, kes vastab "SportClub", on server.
/// </summary>
public static class ServerDiscovery
{
    private const int Port = 5080;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(1500) };

    /// <summary>Kas antud aadressil töötab SportClubi API?</summary>
    public static async Task<bool> IsServerAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            var text = await Http.GetStringAsync(new Uri(new Uri(baseUrl), "api/ping"), ct);
            return text.Contains("SportClub", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Otsib serveri. Tagastab nt "http://192.168.1.243:5080/" või null, kui ei leitud.</summary>
    public static async Task<string?> FindAsync(CancellationToken ct = default)
    {
        var prefixes = LocalPrefixes();
        if (prefixes.Count == 0) return null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var gate = new SemaphoreSlim(48); // korraga kuni 48 päringut
        string? found = null;

        var tasks = prefixes
            .SelectMany(prefix => Enumerable.Range(1, 254).Select(i => $"{prefix}.{i}"))
            .Select(async ip =>
            {
                try
                {
                    await gate.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                try
                {
                    var url = $"http://{ip}:{Port}/";
                    if (await IsServerAsync(url, cts.Token) && Interlocked.CompareExchange(ref found, url, null) is null)
                        cts.Cancel(); // leitud – ülejäänud päringud katkestame
                }
                finally
                {
                    gate.Release();
                }
            });

        await Task.WhenAll(tasks);
        return found;
    }

    /// <summary>Telefoni enda IPv4 võrgud kujul "192.168.1".</summary>
    private static List<string> LocalPrefixes()
    {
        var addresses = new List<IPAddress>();
        try
        {
            addresses.AddRange(NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork));
        }
        catch
        {
            // mõnel platvormil pole see lubatud – proovime allpool teist moodi
        }

        try
        {
            // Ei saada midagi välja: socket lihtsalt näitab, millist kohalikku aadressi kasutataks.
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep) addresses.Insert(0, ep.Address);
        }
        catch
        {
        }

        return addresses
            .Where(IsPrivate)
            .Select(a => string.Join('.', a.GetAddressBytes().Take(3)))
            .Distinct()
            .ToList();
    }

    private static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
