namespace SportClub.Worker;

/// <summary>Every minute: moves the first person in the waitlist to a freed spot (POST /api/waitlist/promote).</summary>
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
                if (result is { Promoted: > 0 } or { Skipped: > 0 })
                    _logger.LogInformation("Waitlist: {Promoted} promoted, {Skipped} skipped", result.Promoted, result.Skipped);
                else
                    _logger.LogDebug("Waitlist: nothing to do");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Waitlist job failed: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Every 15 minutes: sends a reminder for sessions that start within the next hour.</summary>
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
                foreach (var s in sessions.Where(s => s.PendingReminders > 0))
                {
                    var result = await _api.SendReminderAsync(s.SessionId, stoppingToken);
                    _logger.LogInformation("Reminder: {Type} at {StartsAt:HH:mm} UTC → {Sent} sent",
                        s.TrainingTypeName, s.StartsAt, result?.Sent ?? 0);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Reminder job failed: {Message}", ex.Message);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
