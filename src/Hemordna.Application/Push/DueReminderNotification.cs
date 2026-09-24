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
/// The household-local (Europe/Stockholm) wall-clock time the member actually needs to leave -
/// <c>TimeOfDay</c> minus <c>TravelMinutes</c> ONLY. Deliberately NOT the same value as
/// <paramref name="ScheduledFor"/>/<c>ReminderNotificationSelector.notifyTimeOfDay</c>, which is
/// <c>ReminderNotificationSelector.PrepareMinutes</c> minutes EARLIER than this - that gap is the
/// entire point of <c>PrepareMinutes</c> (see <see cref="ReminderNotificationSelector"/>): the
/// notification fires before the real departure time so there is time left to get ready, and
/// this field is what lets <c>SendDueReminderNotifications.BuildText</c> say the real departure
/// time in a <see cref="ReminderNotificationKind.TimeToLeave"/> notification's own text instead
/// of the (earlier) instant the notification itself happens to have fired at - saying the
/// notification's own instant here would tell the member to leave immediately, eating exactly
/// the preparation time <c>PrepareMinutes</c> exists to protect. The same number Min dag's own
/// "Gå HH:mm" row is built from. Always set for <see cref="ReminderNotificationKind.TimeToLeave"/>,
/// always <c>null</c> for <see cref="ReminderNotificationKind.AtTime"/> (nothing to add -
/// <c>TimeOfDay</c> IS the moment that notification names). Already the household-local time - it
/// needs no further timezone conversion to display, see <see cref="ReminderNotificationSelector"/>.
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
