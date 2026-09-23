namespace Hemordna.Domain.Reminders;

/// <summary>
/// Which of the two push notifications a <see cref="Reminder"/> can owe - see docs/PRODUCT.md
/// §11 and <c>Hemordna.Application.Push.ReminderNotificationSelector</c>, which is what actually
/// decides when each one is due. Exactly these two, never more - this task deliberately does not
/// add a "kvällen innan" kind or a configurable set.
/// </summary>
public enum ReminderNotificationKind
{
    /// <summary>
    /// <see cref="Reminder.TimeOfDay"/> minus <see cref="Reminder.TravelMinutes"/> - only ever
    /// due when <see cref="Reminder.TravelMinutes"/> is set.
    /// </summary>
    TimeToLeave = 0,

    /// <summary>At <see cref="Reminder.TimeOfDay"/> itself.</summary>
    AtTime = 1
}
