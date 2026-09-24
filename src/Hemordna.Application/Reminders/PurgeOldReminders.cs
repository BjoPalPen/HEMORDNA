namespace Hemordna.Application.Reminders;

/// <summary>
/// Deletes every reminder whose <see cref="Domain.Reminders.Reminder.Date"/> is more than
/// <see cref="RetentionDays"/> days old - a deliberate product decision: data minimisation
/// (CLAUDE.md §10), not disk space. A reminder's title can read "Läkarbesök" or "Psykolog", and
/// no view ever shows anything older than the current week, so nothing is lost by removing it.
/// </summary>
/// <remarks>
/// Deliberately takes <c>today</c> as an explicit parameter rather than reading a
/// clock itself (CLAUDE.md §5) - exactly the same division of responsibility as
/// <c>Hemordna.Application.Push.SendDueReminderNotifications</c>, which takes <c>now</c>. Only
/// the background service that calls this is allowed to read <see cref="TimeProvider"/>.
/// </remarks>
public sealed class PurgeOldReminders
{
    /// <summary>
    /// How many days a reminder is kept after its own date before it is purged. A reminder
    /// exactly <see cref="RetentionDays"/> days old is kept; one day older is purged - see
    /// <see cref="HandleAsync"/>.
    /// </summary>
    public const int RetentionDays = 30;

    private readonly IReminderRepository _reminders;

    public PurgeOldReminders(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Deletes every reminder with <c>Date &lt; cutoff</c>, where
    /// <c>cutoff = today.AddDays(-<see cref="RetentionDays"/>)</c>. A reminder dated exactly
    /// <see cref="RetentionDays"/> days before <paramref name="today"/> is kept; one dated
    /// <see cref="RetentionDays"/> + 1 days before is purged. Returns how many were deleted.
    /// </summary>
    public async Task<int> HandleAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var cutoff = today.AddDays(-RetentionDays);

        return await _reminders.DeleteOlderThanAsync(cutoff, cancellationToken);
    }
}
