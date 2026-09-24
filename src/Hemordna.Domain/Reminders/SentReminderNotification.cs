using Hemordna.Domain.Common;

namespace Hemordna.Domain.Reminders;

/// <summary>
/// A single, permanent record that one push notification for one <see cref="Reminder"/>, due at
/// one particular instant, was sent - the idempotency ledger behind the reminder push background
/// service (<c>Hemordna.Application.Push.SendDueReminderNotifications</c>). Exists purely so a
/// server restart mid-sweep, or two overlapping sweeps, cannot send the same notification twice.
/// Nothing here is ever read back to display anything - it is write-once bookkeeping, not
/// household history.
/// </summary>
/// <remarks>
/// <para>
/// Keyed on (<see cref="ReminderId"/>, <see cref="Kind"/>, <see cref="ScheduledFor"/>) together,
/// never on (<see cref="ReminderId"/>, <see cref="Kind"/>) alone. The same reminder can
/// legitimately owe two notifications, <see cref="ReminderNotificationKind.TimeToLeave"/> and
/// <see cref="ReminderNotificationKind.AtTime"/>, and each must be sendable independently of
/// whether the other has already gone out - that is what <see cref="Kind"/> is for. Adding
/// <see cref="ScheduledFor"/> to the key on top of that is what makes a moved reminder work: if a
/// reminder is moved (or its travel time changed) after its notification for the OLD instant
/// already fired, the new instant is a different key and has never been recorded as sent, so it
/// is sent again. Deliberately no upsert and no cleanup of the old row here - it simply becomes
/// an inert record of a notification that is no longer due, and is removed later by the same
/// 30-day cleanup that already deletes old reminders (the FK cascade in
/// <c>SentReminderNotificationConfiguration</c>).
/// </para>
/// <para>
/// See <c>SentReminderNotificationConfiguration</c> for the unique index that enforces this
/// triple at the database level too.
/// </para>
/// </remarks>
public sealed class SentReminderNotification
{
    private SentReminderNotification(
        Guid id,
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset sentAt)
    {
        Id = id;
        HouseholdId = householdId;
        ReminderId = reminderId;
        Kind = kind;
        ScheduledFor = scheduledFor;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key - carried along for the same reason every household-reachable row
    /// does (CLAUDE.md §9), even though nothing queries this table by household today.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid ReminderId { get; private set; }

    public ReminderNotificationKind Kind { get; private set; }

    /// <summary>
    /// The due instant this notification was FOR - the same instant
    /// <c>Hemordna.Application.Push.ReminderNotificationSelector</c> computed (<c>TimeOfDay</c>
    /// itself for <see cref="ReminderNotificationKind.AtTime"/>, <c>TimeOfDay</c> minus
    /// <c>TravelMinutes</c> for <see cref="ReminderNotificationKind.TimeToLeave"/>) when it
    /// produced the <c>DueReminderNotification</c> this row records. Together with
    /// <see cref="ReminderId"/> and <see cref="Kind"/> this is the actual idempotency key - see
    /// this class's remarks. Not to be confused with <see cref="SentAt"/>, which is WHEN the
    /// send happened, not what it was for.
    /// </summary>
    public DateTimeOffset ScheduledFor { get; private set; }

    /// <summary>
    /// The instant the background service recorded this - i.e. WHEN the send happened. Not to be
    /// confused with <see cref="ScheduledFor"/>, which is the due instant the notification was
    /// FOR; the two are only ever close together because <c>SendDueReminderNotifications.DueWindow</c>
    /// is narrow, never because they mean the same thing.
    /// </summary>
    public DateTimeOffset SentAt { get; private set; }

    public static SentReminderNotification Create(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset sentAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(reminderId, nameof(reminderId));

        return new SentReminderNotification(
            Guid.NewGuid(), householdId, reminderId, kind, scheduledFor, sentAt);
    }
}
