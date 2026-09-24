using Hemordna.Application.Reminders;
using Hemordna.Application.Time;

namespace Hemordna.Api.Services;

/// <summary>
/// Periodically purges reminders older than <see cref="PurgeOldReminders.RetentionDays"/> days -
/// see docs/PRODUCT.md §11 and <see cref="PurgeOldReminders"/>, which does the actual work. This
/// class's only job is the two things <see cref="PurgeOldReminders"/> deliberately cannot do
/// itself (CLAUDE.md §5): read the clock, and own the loop.
/// </summary>
/// <remarks>
/// <para>
/// <b>Interval: 24 hours, and it runs once immediately at startup</b> - the same loop shape as
/// <see cref="ReminderPushBackgroundService"/> already gives that for free (the first iteration
/// runs before the first delay). A day-granularity job does not need to run more often: reminder
/// dates are whole days, not instants, so nothing is gained by polling faster - it would just run
/// 288 times a day instead of once for no benefit.
/// </para>
/// <para>
/// Kept as its own class rather than folded into <see cref="ReminderPushBackgroundService"/> -
/// that class's own XML doc says explicitly that its only job is what
/// <c>SendDueReminderNotifications</c> cannot do itself, and a cleanup job does not belong there.
/// Same overall shape otherwise: scope-per-sweep, swallow-and-log around each sweep so one bad
/// run cannot kill the loop.
/// </para>
/// </remarks>
public sealed class ReminderCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReminderCleanupBackgroundService> _logger;

    public ReminderCleanupBackgroundService(
        IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ReminderCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reminder cleanup background service started; running every {PollInterval}.", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deletedCount = await RunOnceAsync(stoppingToken);

                if (deletedCount > 0)
                {
                    // Count only - never a title, location, ReminderId, MemberId or HouseholdId.
                    // That is exactly the personal data this purge exists to get rid of
                    // (CLAUDE.md §10, docs/PRODUCT.md §11); a count is enough to see the job is alive.
                    _logger.LogInformation("Purged {Count} old reminder(s).", deletedCount);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Reminder cleanup sweep failed.");
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
        var purgeOldReminders = scope.ServiceProvider.GetRequiredService<PurgeOldReminders>();

        // The only clock read in this whole feature - HouseholdClock.Today owns what "today"
        // means (including the UTC fallback if the timezone database is missing - see
        // HouseholdClock's own remarks); this class has no opinion of its own about that and adds
        // none. PurgeOldReminders itself never reads a clock (CLAUDE.md §5).
        var today = HouseholdClock.Today(_timeProvider);

        return await purgeOldReminders.HandleAsync(today, cancellationToken);
    }
}
