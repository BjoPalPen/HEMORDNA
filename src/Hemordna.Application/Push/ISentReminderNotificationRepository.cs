using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// The idempotency ledger behind <see cref="SendDueReminderNotifications"/> - persists which
/// (<c>ReminderId</c>, <c>Kind</c>) pairs have already been sent, so a server restart mid-sweep
/// or two overlapping sweeps cannot send the same notification twice.
/// </summary>
public interface ISentReminderNotificationRepository
{
    /// <summary>
    /// Every (ReminderId, Kind) pair already recorded as sent, among <paramref name="reminderIds"/> -
    /// one query per sweep instead of one per candidate notification.
    /// </summary>
    Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken);

    /// <summary>
    /// Records that this reminder's notification of this kind was sent. Must be called before
    /// the actual push send is attempted - see <see cref="SendDueReminderNotifications"/> for why.
    /// </summary>
    Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
