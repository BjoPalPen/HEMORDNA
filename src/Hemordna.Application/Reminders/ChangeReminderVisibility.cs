using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Changes what the rest of the household can see of one of the caller's own reminders.</summary>
public sealed class ChangeReminderVisibility
{
    private readonly IReminderRepository _reminders;

    public ChangeReminderVisibility(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Changes the visibility, or returns <c>null</c> when the household has no such reminder OR
    /// the reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose: only the owner may ever change who can
    /// see their own reminder (docs/PRODUCT.md §8), so a caller who does not own it gets exactly
    /// the same "not found" result as an id that does not exist, never a hint that it does. A rule
    /// violation from the domain (a cancelled or checked-off reminder) is left to throw uncaught,
    /// same as <see cref="ChangeReminderTitle"/> and <see cref="ChangeReminderLocation"/>.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        ReminderVisibility visibility,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.ChangeVisibility(visibility);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
