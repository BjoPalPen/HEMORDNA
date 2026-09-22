using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>
/// Reads a member's own reminders due in a date range - Min dag (a single day) and Vecka (a
/// week). Deliberately just this member's own: a reminder never appears in the household view
/// or in anyone else's Vecka (docs/PRODUCT.md §11).
/// </summary>
public sealed class GetOwnReminders
{
    private readonly IReminderRepository _reminders;

    public GetOwnReminders(IReminderRepository reminders) => _reminders = reminders;

    public Task<IReadOnlyList<Reminder>> HandleAsync(
        Guid householdId,
        Guid memberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
        => _reminders.ListForMemberInRangeAsync(householdId, memberId, fromDate, toDate, cancellationToken);
}
