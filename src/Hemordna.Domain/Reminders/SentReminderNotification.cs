using Hemordna.Domain.Common;

namespace Hemordna.Domain.Reminders;

/// <summary>
/// A single, permanent record that one push notification for one <see cref="Reminder"/> was
/// sent - the idempotency ledger behind the reminder push background service
/// (<c>Hemordna.Application.Push.SendDueReminderNotifications</c>). Exists purely so a server
/// restart mid-sweep, or two overlapping sweeps, cannot send the same notification twice.
/// Nothing here is ever read back to display anything - it is write-once bookkeeping, not
/// household history.
/// </summary>
/// <remarks>
/// Keyed on (<see cref="ReminderId"/>, <see cref="Kind"/>) together, never on
/// <see cref="ReminderId"/> alone: the same reminder can legitimately owe two notifications,
/// <see cref="ReminderNotificationKind.TimeToLeave"/> and
/// <see cref="ReminderNotificationKind.AtTime"/>, and each must be sendable independently of
/// whether the other has already gone out. See <c>SentReminderNotificationConfiguration</c> for
/// the unique index that enforces this at the database level too.
/// </remarks>
public sealed class SentReminderNotification
{
    private SentReminderNotification(
        Guid id,
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset sentAt)
    {
        Id = id;
        HouseholdId = householdId;
        ReminderId = reminderId;
        Kind = kind;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key - carried along for the same reason every household-reachable row
    /// does (CLAUDE.md §9), even though nothing queries this table by household today.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid ReminderId { get; private set; }

    public ReminderNotificationKind Kind { get; private set; }

    /// <summary>The instant the background service recorded this - not necessarily the
    /// notification's own due instant, which lives on the <see cref="Reminder"/> itself.</summary>
    public DateTimeOffset SentAt { get; private set; }

    public static SentReminderNotification Create(
        Guid householdId, Guid reminderId, ReminderNotificationKind kind, DateTimeOffset sentAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(reminderId, nameof(reminderId));

        return new SentReminderNotification(Guid.NewGuid(), householdId, reminderId, kind, sentAt);
    }
}
