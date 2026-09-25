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
    /// Other household members' reminders visible to someone who is not their owner - Vecka's
    /// "Andras tider" section (<c>GetHouseholdReminders</c>). Filters to
    /// <see cref="Reminder.Visibility"/> != <see cref="ReminderVisibility.Private"/> and excludes
    /// <paramref name="excludeMemberId"/>'s own reminders - without that exclusion, the caller's
    /// own reminders would be duplicated here alongside "Dina påminnelser"
    /// (<see cref="ListForMemberInRangeAsync"/> already returns those, in full, with
    /// <see cref="Reminder.Location"/> and everything else this method's caller must never see).
    /// <see cref="ReminderStatus.Cancelled"/> is excluded, same as
    /// <see cref="ListForMemberInRangeAsync"/>. A <see cref="ReminderStatus.CheckedOff"/> reminder
    /// IS included, but that is a filtering fact only - <see cref="Reminder.Status"/> itself never
    /// leaves the owner (docs/PRODUCT.md §8), so a checked-off time must look identical to any
    /// other time to whoever reads the result of this call; nothing in it says which reminders
    /// were checked off.
    /// </summary>
    Task<IReadOnlyList<Reminder>> ListVisibleForOthersInRangeAsync(
        Guid householdId,
        Guid excludeMemberId,
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

    /// <summary>
    /// Deletes every reminder - any household, any member, any <see cref="ReminderStatus"/> -
    /// whose <see cref="Reminder.Date"/> is strictly before <paramref name="cutoff"/>, and
    /// returns how many were removed. Data minimisation (CLAUDE.md §10), not disk space: a
    /// reminder's title can read "Läkarbesök" or "Psykolog", and no view ever shows anything
    /// older than the current week (Min dag fetches [Today, Today], Vecka fetches the current
    /// Monday–Sunday), so nothing in the app ever reads a row this old again.
    /// <para>
    /// Deliberately global, not household-scoped, exactly like <see cref="ListInDateRangeAsync"/>
    /// - this is a system maintenance job, not an API-reachable operation, so CLAUDE.md §9's
    /// household boundary (which applies to what a user can reach) does not apply here.
    /// </para>
    /// <para>
    /// Every status is deleted alike - an <see cref="ReminderStatus.Upcoming"/> reminder from 40
    /// days ago is exactly as dead as a checked-off one; nothing distinguishes them once the
    /// date has passed.
    /// </para>
    /// <para>
    /// <see cref="SentReminderNotification"/> rows for a deleted reminder are removed
    /// automatically by the FK cascade in <c>SentReminderNotificationConfiguration</c>
    /// (<c>OnDelete(DeleteBehavior.Cascade)</c> on <c>ReminderId</c>) - that log needs no purge
    /// of its own.
    /// </para>
    /// </summary>
    Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken cancellationToken);
}
