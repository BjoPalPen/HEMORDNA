using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Takes back a cancellation on one of the caller's own reminders.</summary>
public sealed class RestoreReminder
{
    private readonly IReminderRepository _reminders;

    public RestoreReminder(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Restores the reminder, or returns <c>null</c> when the household has no such reminder OR
    /// the reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose. A rule violation from the domain (the
    /// reminder is not cancelled) is left to throw uncaught.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.Restore();

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
