using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// The idempotency ledger behind <see cref="SendDueReminderNotifications"/> - persists which
/// (<c>ReminderId</c>, <c>Kind</c>, <c>ScheduledFor</c>) triples have already been sent, so a
/// server restart mid-sweep or two overlapping sweeps cannot send the same notification twice.
/// <c>ScheduledFor</c> - the due instant a notification is FOR, see
/// <see cref="DueReminderNotification.ScheduledFor"/> - is part of the key precisely so that a
/// reminder moved (or re-timed) to a new instant after its old notification already fired is
/// never mistaken for a duplicate: the new instant is a new key nobody has recorded yet. No code
/// path needs to remember to clear anything when a reminder's time changes - see
/// <c>Hemordna.Domain.Reminders.SentReminderNotification</c> for the full reasoning.
/// </summary>
public interface ISentReminderNotificationRepository
{
    /// <summary>
    /// Every (ReminderId, Kind, ScheduledFor) triple already recorded as sent, among
    /// <paramref name="reminderIds"/> - one query per sweep instead of one per candidate
    /// notification.
    /// </summary>
    Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind, DateTimeOffset ScheduledFor)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken);

    /// <summary>
    /// Records that this reminder's notification of this kind, due at <paramref name="scheduledFor"/>,
    /// was sent. Must be called before the actual push send is attempted - see
    /// <see cref="SendDueReminderNotifications"/> for why.
    /// </summary>
    Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);
}
