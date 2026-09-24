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
/// <b>Poll interval: 1 minute.</b> Raised from 5 minutes after a real production incident: on
/// 2026-09-24, Björn's own <see cref="Hemordna.Domain.Reminders.ReminderNotificationKind.TimeToLeave"/>
/// notification (a doctor's appointment, <c>ScheduledFor</c> 16:45:00) was sent at 16:49:44 -
/// 4 minutes 44 seconds late, an artefact of the 5-minute sweep, not a bug in the selection
/// logic. That lateness is NOT acceptable for this particular notification, even though the
/// previous 5-minute figure was defended on exactly that basis: "vid tiden" tolerates arriving
/// late because it means "around now", but "time to leave" is a deadline counted backwards from
/// a meeting - every late minute is eaten directly out of the travel margin the notification
/// exists to protect, which is the one thing a member cannot recover once the notification has
/// already arrived. <see cref="SendDueReminderNotifications.DueWindow"/> (15 minutes) is kept as
/// is: it is already comfortably wider than this interval, which is what a sweep interval needs
/// to be (otherwise an instant could fall between two sweeps and never be seen as due at all),
/// and it still protects against a burst of stale notifications after a real outage - see
/// CLAUDE.md §8 and this class's own <see cref="RunOnceAsync"/> for that boundary.
/// </para>
/// <para>
/// The extra cost of sweeping twelve times as often is negligible: each sweep is one indexed
/// query over a couple of days' worth of reminders (<see cref="SendDueReminderNotifications"/>'s
/// own lookahead range), and on almost every sweep it finds nothing due at all. There is no
/// reason to trade a member's travel margin for a query this cheap.
/// </para>
/// <para>
/// Same overall shape as BowlingPlatform's <c>MeetingReminderService</c> (scope-per-sweep,
/// swallow-and-log around each sweep so one bad run cannot kill the loop) - but not its cadence.
/// That service runs once a day and compares whole days; this one runs every minute and
/// compares exact instants, because a <c>Reminder.TimeOfDay</c> is a clock time, not a date.
/// </para>
/// </remarks>
public sealed class ReminderPushBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

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
