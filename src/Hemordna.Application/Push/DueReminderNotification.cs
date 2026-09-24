using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// One reminder push notification that is due to be sent right now, as computed by
/// <see cref="ReminderNotificationSelector"/>. Carries everything <see cref="IPushSender"/>
/// needs for its payload and everything <see cref="ISentReminderNotificationRepository"/> needs
/// to record it - see docs/PRODUCT.md §11 and CLAUDE.md §8: no field here is ever used to say a
/// member is late.
/// </summary>
/// <param name="ScheduledFor">
/// The due instant this notification is for, truncated to the minute by
/// <see cref="ReminderNotificationSelector"/> - see that class for why. Together with
/// <paramref name="ReminderId"/> and <paramref name="Kind"/> this is the idempotency key
/// <see cref="ISentReminderNotificationRepository"/> checks and records: a reminder moved (or
/// re-timed) to a new instant after its old notification already fired is a new key, so it is
/// never mistaken for a duplicate.
/// </param>
public sealed record DueReminderNotification(
    Guid ReminderId,
    Guid HouseholdId,
    Guid MemberId,
    ReminderNotificationKind Kind,
    DateTimeOffset ScheduledFor,
    string Title,
    string? Location);
