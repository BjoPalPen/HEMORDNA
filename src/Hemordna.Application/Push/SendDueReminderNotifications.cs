using Hemordna.Application.Reminders;
using Hemordna.Application.Time;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// Sends every reminder push notification that is due right now. The orchestration for the
/// reminder push background service: gathers candidates, runs them through the pure
/// <see cref="ReminderNotificationSelector"/>, filters out what <see cref="ISentReminderNotificationRepository"/>
/// says already went out, and sends the rest.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately takes <c>now</c> as an explicit parameter rather than a <see cref="TimeProvider"/>
/// - unlike most use cases in this codebase (for example <c>CreateHousehold</c>). CLAUDE.md §5:
/// "logiken får inte [läsa klockan]" - only the background service that calls this is allowed to
/// read <see cref="TimeProvider.GetUtcNow"/>; this class cannot even accidentally do so, because
/// it has nothing to read it from.
/// </para>
/// <para>
/// <b>Idempotency, and which way it leans.</b>
/// <see cref="ISentReminderNotificationRepository.MarkSentAsync"/> is called AFTER
/// <see cref="IPushSender.SendAsync"/>, deliberately. That makes this at-least-once: a transient
/// failure at the push service leaves the notification unmarked, so the next sweep tries again
/// while <see cref="DueWindow"/> is still open, and a crash in between the send and the mark can
/// deliver the same notification twice.
/// <para>
/// Björn's decision, and the right way round for THIS feature: a duplicate "Dags att gå" is a
/// moment's irritation, while a missed one means arriving late - which is the exact failure the
/// whole reminder feature exists to prevent. Marking first would trade that annoyance for silent
/// loss whenever Apple's push service hiccups. Do not "tidy" this back into mark-then-send.
/// </para>
/// </para>
/// <para>
/// A notification for a member with zero live push subscriptions is never marked sent - nothing
/// was actually delivered, so there is nothing to remember. It is naturally retried on the next
/// sweep(s), until <see cref="DueWindow"/> closes it out on its own.
/// </para>
/// </remarks>
public sealed class SendDueReminderNotifications
{
    /// <summary>
    /// How far past "due" a notification may still be sent before it is skipped instead - the
    /// cutoff. Must be at least as wide as the background service's poll interval (5 minutes,
    /// see the background service itself) or an instant can fall between two sweeps and never be
    /// seen as due at all. Kept to three times that (15 minutes) rather than wider: after a real
    /// outage, the goal is exactly one sweep's worth of catch-up, not a burst of stale
    /// notifications for everything that piled up while the server was down - see CLAUDE.md §8.
    /// </summary>
    public static readonly TimeSpan DueWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How far around "today" to fetch reminder candidates from the database. One day each way
    /// is generous slack for TimeOfDay combined with TravelMinutes (which can push the
    /// "time to leave" instant into the previous day) and for the DST-transition day itself,
    /// while still being a cheap, bounded query - see <see cref="IReminderRepository.ListInDateRangeAsync"/>.
    /// </summary>
    private const int LookaheadDays = 1;

    private readonly IReminderRepository _reminders;
    private readonly ISentReminderNotificationRepository _sentLog;
    private readonly IPushSubscriptionRepository _subscriptions;
    private readonly IPushSender _sender;

    public SendDueReminderNotifications(
        IReminderRepository reminders,
        ISentReminderNotificationRepository sentLog,
        IPushSubscriptionRepository subscriptions,
        IPushSender sender)
    {
        _reminders = reminders;
        _sentLog = sentLog;
        _subscriptions = subscriptions;
        _sender = sender;
    }

    /// <summary>Returns how many individual devices actually received a notification this sweep.</summary>
    public async Task<int> HandleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = HouseholdClock.Today(now);

        var candidates = await _reminders.ListInDateRangeAsync(
            today.AddDays(-LookaheadDays), today.AddDays(LookaheadDays), cancellationToken);

        var due = ReminderNotificationSelector.SelectDue(now, DueWindow, candidates);

        if (due.Count == 0)
        {
            return 0;
        }

        var alreadySent = await _sentLog.ListSentAsync(
            [.. due.Select(notification => notification.ReminderId).Distinct()], cancellationToken);

        var deliveredCount = 0;

        foreach (var notification in due)
        {
            if (alreadySent.Contains((notification.ReminderId, notification.Kind)))
            {
                continue;
            }

            var subscriptions = await _subscriptions.ListForMemberAsync(
                notification.HouseholdId, notification.MemberId, cancellationToken);

            if (subscriptions.Count == 0)
            {
                continue;
            }

            var (title, body) = BuildText(notification);
            var delivered = await _sender.SendAsync(subscriptions, title, body, "/", cancellationToken);

            if (delivered == 0)
            {
                // Nothing actually reached a device, so there is nothing to remember - the next
                // sweep tries again while DueWindow is open. See this class's remarks.
                continue;
            }

            // Recorded only once something was delivered - see this class's remarks on idempotency.
            await _sentLog.MarkSentAsync(
                notification.HouseholdId, notification.ReminderId, notification.Kind, now, cancellationToken);

            deliveredCount += delivered;
        }

        return deliveredCount;
    }

    /// <summary>
    /// Builds the notification text. §8 applies without exception - neither branch may ever
    /// imply someone is late; both are purely forward-looking ("it's time to leave", "it's now").
    /// </summary>
    private static (string Title, string Body) BuildText(DueReminderNotification notification) => notification.Kind switch
    {
        // Generic title, so the notification is useful at a glance even before it's opened; the
        // reminder's own title and location (docs/PRODUCT.md §11) carry the specifics.
        ReminderNotificationKind.TimeToLeave => (
            "Dags att gå",
            notification.Location is null
                ? notification.Title
                : $"{notification.Title} – {notification.Location}"),

        // Björn's explicit decision (docs/PRODUCT.md §11): the reminder's own title is the
        // notification's title, even though that means it can show on a locked screen.
        ReminderNotificationKind.AtTime => (
            notification.Title,
            notification.Location is null ? "Nu." : $"Nu – {notification.Location}."),

        _ => throw new ArgumentOutOfRangeException(nameof(notification), notification.Kind, "Unknown notification kind.")
    };
}
