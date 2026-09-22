using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Moves one of the caller's own reminders to a new date and time of day.</summary>
public sealed class MoveReminder
{
    private readonly IReminderRepository _reminders;

    public MoveReminder(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Moves the reminder, or returns <c>null</c> when the household has no such reminder OR the
    /// reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose. A rule violation from the domain (a
    /// cancelled reminder, or a date outside the supported calendar) is left to throw uncaught.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        DateOnly date,
        TimeOnly? timeOfDay,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.MoveTo(date, timeOfDay);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
