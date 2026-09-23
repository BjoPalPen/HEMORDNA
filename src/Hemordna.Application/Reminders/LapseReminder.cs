using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Checks off one of the caller's own reminders - see <see cref="Reminder.Lapse"/>.</summary>
public sealed class LapseReminder
{
    private readonly IReminderRepository _reminders;

    public LapseReminder(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Lapses the reminder, or returns <c>null</c> when the household has no such reminder OR
    /// the reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose. A rule violation from the domain (a
    /// cancelled reminder cannot be checked off) is left to throw uncaught.
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

        reminder.Lapse();

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
