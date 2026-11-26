namespace SportClub.Maui.Services;

/// <summary>
/// Kirjutab käsitlemata vead faili crash.log (Windowsis programmi kausta bin\...\crash.log),
/// et oleks näha, miks rakendus kinni läks.
/// </summary>
public static class CrashLog
{
    private static readonly object Gate = new();

    public static string FilePath
    {
        get
        {
            try
            {
                var dir = AppContext.BaseDirectory;
                var probe = Path.Combine(dir, "crash.log");
                File.AppendAllText(probe, ""); // kas kaust on kirjutatav
                return probe;
            }
            catch
            {
                return Path.Combine(FileSystem.AppDataDirectory, "crash.log");
            }
        }
    }

    /// <summary>Kuulab kõiki käsitlemata vigu (ka async ülesannete omi).</summary>
    public static void Init()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write(e.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write(e.Exception, "Task");
            e.SetObserved();
        };
    }

    public static void Write(Exception? ex, string source)
    {
        if (ex is null) return;
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}\n\n");
        }
        catch
        {
            // logimine ei tohi ise rakendust maha võtta
        }
        System.Diagnostics.Debug.WriteLine($"[CrashLog] {source}: {ex}");
    }
}
