using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Sets or clears the travel time on one of the caller's own reminders.</summary>
public sealed class SetReminderTravelMinutes
{
    private readonly IReminderRepository _reminders;

    public SetReminderTravelMinutes(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Sets or clears the travel time, or returns <c>null</c> when the household has no such
    /// reminder OR the reminder belongs to a different member - see
    /// <see cref="ChangeReminderTitle"/> for why those two cases are indistinguishable on
    /// purpose. A rule violation from the domain (a cancelled reminder, travel time without a
    /// time of day, or a value outside the supported range) is left to throw uncaught.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        int? travelMinutes,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.SetTravelMinutes(travelMinutes);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
