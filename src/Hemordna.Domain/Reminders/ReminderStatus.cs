namespace Hemordna.Domain.Reminders;

/// <summary>The lifecycle of a <see cref="Reminder"/>.</summary>
public enum ReminderStatus
{
    /// <summary>Still ahead - has not been cancelled.</summary>
    Upcoming = 0,

    /// <summary>Called off by its owner. A cancelled reminder cannot be changed.</summary>
    Cancelled = 1
}
