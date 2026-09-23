using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// One reminder push notification that is due to be sent right now, as computed by
/// <see cref="ReminderNotificationSelector"/>. Carries everything <see cref="IPushSender"/>
/// needs for its payload and everything <see cref="ISentReminderNotificationRepository"/> needs
/// to record it - see docs/PRODUCT.md §11 and CLAUDE.md §8: no field here is ever used to say a
/// member is late.
/// </summary>
public sealed record DueReminderNotification(
    Guid ReminderId,
    Guid HouseholdId,
    Guid MemberId,
    ReminderNotificationKind Kind,
    string Title,
    string? Location);
