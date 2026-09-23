using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Persists a member's own reminders.</summary>
public interface IReminderRepository
{
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken);

    /// <summary>
    /// The reminder if it exists in this household, or <c>null</c>. Deliberately does not check
    /// <see cref="Reminder.MemberId"/> - a reminder is private to its owner (docs/PRODUCT.md §11,
    /// CLAUDE.md §9), so every use case that calls this must additionally verify
    /// <see cref="Reminder.MemberId"/> against the caller and treat a mismatch identically to
    /// "not found": return <c>null</c>, never throw, never reveal that the reminder exists.
    /// </summary>
    Task<Reminder?> FindByIdAsync(Guid householdId, Guid reminderId, CancellationToken cancellationToken);

    Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken);

    /// <summary>
    /// This member's own reminders due within [<paramref name="fromDate"/>, <paramref name="toDate"/>] -
    /// Min dag and Vecka. Cancelled reminders are excluded: a cancelled reminder "lämnar dagen utan
    /// markering" (docs/PRODUCT.md §11), so it has nothing to show anywhere. A checked-off
    /// (<see cref="ReminderStatus.CheckedOff"/>) reminder is deliberately NOT excluded here - unlike a
    /// cancellation, checking one off leaves a visible marking on its day, the same way a
    /// completed task stays visible rather than disappearing. Scoped by both
    /// <paramref name="householdId"/> and <paramref name="memberId"/> - unlike
    /// <see cref="FindByIdAsync"/>, there is no single id to look up here, so the privacy boundary
    /// must be enforced directly in the query.
    /// </summary>
    Task<IReadOnlyList<Reminder>> ListForMemberInRangeAsync(
        Guid householdId,
        Guid memberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every reminder - any household, any member, any <see cref="ReminderStatus"/>, "all day"
    /// included - whose <see cref="Reminder.Date"/> falls in [<paramref name="fromDate"/>,
    /// <paramref name="toDate"/>]. Used by the reminder push background service
    /// (<c>Hemordna.Application.Push.SendDueReminderNotifications</c>) to gather candidates
    /// around "now" without scanning the whole table. Deliberately unfiltered on status or
    /// <see cref="Reminder.TimeOfDay"/>, unlike <see cref="ListForMemberInRangeAsync"/> - that
    /// filtering is <c>ReminderNotificationSelector</c>'s job, not this query's, so it stays
    /// testable as a pure function against exactly the same candidate shapes this method
    /// returns.
    /// </summary>
    Task<IReadOnlyList<Reminder>> ListInDateRangeAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
