using Hemordna.Application.Push;

namespace Hemordna.Api.Services;

/// <summary>
/// Periodically sends every reminder push notification that has become due - see
/// docs/PRODUCT.md §11 and <see cref="SendDueReminderNotifications"/>, which does the actual
/// work. This class's only job is the two things <see cref="SendDueReminderNotifications"/>
/// deliberately cannot do itself (CLAUDE.md §5): read the clock, and own the loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Poll interval: 5 minutes.</b> That means a notification can arrive up to 5 minutes after
/// its instant. Acceptable here: neither "dags att gå" nor "vid tiden" is a precision alarm -
/// both already build in their own slack (a reminder's travel time is itself an estimate,
/// and "vid tiden" is "around now", not a countdown) - so a few minutes of latency does not
/// undermine the point of either notification. <see cref="SendDueReminderNotifications.DueWindow"/>
/// (15 minutes) is kept wider than this interval specifically so that latency never becomes a
/// missed notification: even a slow or overlapping sweep still finds a not-yet-expired instant
/// on its next pass.
/// </para>
/// <para>
/// Same overall shape as BowlingPlatform's <c>MeetingReminderService</c> (scope-per-sweep,
/// swallow-and-log around each sweep so one bad run cannot kill the loop) - but not its cadence.
/// That service runs once a day and compares whole days; this one runs every few minutes and
/// compares exact instants, because a <c>Reminder.TimeOfDay</c> is a clock time, not a date.
/// </para>
/// </remarks>
public sealed class ReminderPushBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderPushBackgroundService> _logger;

    public ReminderPushBackgroundService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ReminderPushBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reminder push background service started; polling every {PollInterval}.", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delivered = await RunOnceAsync(stoppingToken);

                if (delivered > 0)
                {
                    _logger.LogInformation("Sent {Count} reminder push notification(s).", delivered);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminder push sweep failed.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown - the while-condition above ends the loop next iteration.
            }
        }
    }

    internal async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sendDueReminderNotifications = scope.ServiceProvider.GetRequiredService<SendDueReminderNotifications>();

        // The only clock read in this whole feature - everything downstream (which reminders
        // are due, what "today" means for the candidate query) takes this same instant as an
        // explicit parameter instead of reading a clock itself. See CLAUDE.md §5.
        var now = _timeProvider.GetUtcNow();

        return await sendDueReminderNotifications.HandleAsync(now, cancellationToken);
    }
}
