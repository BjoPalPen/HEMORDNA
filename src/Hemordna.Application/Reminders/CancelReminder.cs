using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Cancels one of the caller's own reminders. Idempotent - see <see cref="Reminder.Cancel"/>.</summary>
public sealed class CancelReminder
{
    private readonly IReminderRepository _reminders;

    public CancelReminder(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Cancels the reminder, or returns <c>null</c> when the household has no such reminder OR
    /// the reminder belongs to a different member - see
    /// <see cref="ChangeReminderTitle"/> for why those two cases are indistinguishable on
    /// purpose.
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

        reminder.Cancel();

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
