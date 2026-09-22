using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Changes the title of one of the caller's own reminders.</summary>
public sealed class ChangeReminderTitle
{
    private readonly IReminderRepository _reminders;

    public ChangeReminderTitle(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// Changes the title, or returns <c>null</c> when the household has no such reminder OR the
    /// reminder belongs to a different member - the two cases are handled identically on
    /// purpose: a reminder is private to its owner (docs/PRODUCT.md §11, CLAUDE.md §9), so a
    /// caller who does not own it must see exactly the same "not found" result they would get
    /// for an id that does not exist at all, never a hint that it does. A rule violation from
    /// the domain (a cancelled reminder) is left to throw uncaught, same as
    /// <see cref="Tasks.DeferTaskOccurrence"/>.
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        string title,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        reminder.ChangeTitle(title);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
