namespace Hemordna.Domain.Reminders;

/// <summary>
/// How much of a <see cref="Reminder"/> the rest of the household can see. Always chosen by the
/// owner (see <see cref="Reminder.ChangeVisibility"/>) and never by anyone else - a shared time is
/// information for other members, not a checkbox for them (docs/PRODUCT.md §8). Whatever the
/// level, <see cref="Reminder.Location"/> never leaves the owner, and neither does
/// <see cref="Reminder.Status"/>, <see cref="Reminder.TravelMinutes"/> or
/// <see cref="Reminder.CreatedAt"/> - a checked-off reminder looks like any other time to someone
/// else, and a cancelled one is not visible at all.
/// </summary>
public enum ReminderVisibility
{
    /// <summary>Default. Nobody but the owner sees this reminder exists.</summary>
    Private = 0,

    /// <summary>
    /// Other household members see that the owner has a time on this date - never the title,
    /// never the location. Enough to explain a gap in the day without saying what it is.
    /// </summary>
    BusyOnly = 1,

    /// <summary>
    /// Other household members see the time and the title. The location still never leaves the
    /// owner - see the class remarks.
    /// </summary>
    Household = 2
}
