namespace SportClub.Worker;

/// <summary>Iga minuti järel: viib järjekorras esimese vabanenud kohale (POST /api/waitlist/promote).</summary>
public class WaitlistPromotionJob : BackgroundService
{
    private readonly ApiClient _api;
    private readonly WorkerOptions _options;
    private readonly ILogger<WaitlistPromotionJob> _logger;

    public WaitlistPromotionJob(ApiClient api, WorkerOptions options, ILogger<WaitlistPromotionJob> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.WaitlistIntervalSeconds));
        do
        {
            try
            {
                var result = await _api.PromoteWaitlistAsync(stoppingToken);
                if (result is null || (result.Promoted.Count == 0 && result.Removed == 0))
                    _logger.LogDebug("Waitlist: nothing to do");
                else
                {
                    foreach (var p in result.Promoted)
                        _logger.LogInformation("Waitlist: {Name} → session {SessionId}", p.Name, p.SessionId);
                    if (result.Removed > 0)
                        _logger.LogInformation("Waitlist: {Removed} removed from queue", result.Removed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Waitlist job failed: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Iga 15 minuti järel: saadab meeldetuletuse treeningutele, mis algavad tunni jooksul.</summary>
public class ReminderJob : BackgroundService
{
    private readonly ApiClient _api;
    private readonly WorkerOptions _options;
    private readonly ILogger<ReminderJob> _logger;

    public ReminderJob(ApiClient api, WorkerOptions options, ILogger<ReminderJob> logger)
    {
        _api = api;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.ReminderIntervalMinutes));
        do
        {
            try
            {
                var sessions = await _api.GetStartingSessionsAsync(_options.ReminderWithinMinutes, stoppingToken) ?? new();
                foreach (var s in sessions)
                foreach (var p in s.Participants)
                {
                    var result = await _api.SendReminderAsync(p.BookingId, stoppingToken);
                    if (result is not null)
                        _logger.LogInformation("Reminder: {Type} {StartsAt} → {Name} ({Email})", s.Type, s.StartsAt, p.Name, p.Email);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Reminder job failed: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
