using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Changes the location of one of the caller's own reminders.</summary>
public sealed class ChangeReminderLocation
{
    private readonly IReminderRepository _reminders;

    public ChangeReminderLocation(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Changes the location, or returns <c>null</c> when the household has no such reminder OR
    /// the reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose. A rule violation from the domain (a
    /// cancelled reminder) is left to throw uncaught.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        string? location,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.ChangeLocation(location);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
