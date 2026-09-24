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
/// <param name="DepartureTimeOfDay">
/// The household-local (Europe/Stockholm) wall-clock departure time - <c>TimeOfDay</c> minus
/// <c>TravelMinutes</c> minus <c>ReminderNotificationSelector.PrepareMinutes</c> - to show in a
/// <see cref="ReminderNotificationKind.TimeToLeave"/> notification's own text (see
/// <c>SendDueReminderNotifications.BuildText</c>): since that notification can now arrive up to
/// <see cref="ReminderNotificationSelector.PrepareMinutes"/> minutes (and, inside
/// <c>SendDueReminderNotifications.DueWindow</c>, later still) before the moment it names, the
/// text needs to say the actual departure time rather than implying "right now". Always set for
/// <see cref="ReminderNotificationKind.TimeToLeave"/>, always <c>null</c> for
/// <see cref="ReminderNotificationKind.AtTime"/> (nothing to add - <c>TimeOfDay</c> IS the
/// moment the notification names). Already the household-local time - it needs no further
/// timezone conversion to display, see <see cref="ReminderNotificationSelector"/>.
/// </param>
public sealed record DueReminderNotification(
    Guid ReminderId,
    Guid HouseholdId,
    Guid MemberId,
    ReminderNotificationKind Kind,
    DateTimeOffset ScheduledFor,
    string Title,
    string? Location,
    TimeOnly? DepartureTimeOfDay);
