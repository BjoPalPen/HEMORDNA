namespace Hemordna.Domain.Reminders;

/// <summary>The lifecycle of a <see cref="Reminder"/>.</summary>
public enum ReminderStatus
{
    /// <summary>Still ahead - has not been cancelled.</summary>
    Upcoming = 0,

    /// <summary>Called off by its owner. A cancelled reminder cannot be changed.</summary>
    Cancelled = 1,

    /// <summary>
    /// Its owner has said this time no longer needs reminding about - the appointment already
    /// happened, or otherwise no longer matters. Named for what happened to the TIME, not to a
    /// chore, on purpose: unlike <see cref="Hemordna.Domain.Tasks.TaskOccurrenceStatus.Completed"/>,
    /// this is never household work and must never be read as one (docs/PRODUCT.md §11, CLAUDE.md
    /// §8) - "Lapsed" says the moment has run out, the same way a lapsed subscription or a
    /// lapsed deadline does, without implying anyone did or didn't do something. It is also
    /// deliberately not the same as a reminder's time simply passing unattended (PRODUCT.md §11:
    /// "En passerad tid blir tyst" - that stays <see cref="Upcoming"/> and leaves the day without
    /// any marking at all): <c>Lapsed</c> is the owner's own, explicit "jag har bockat av den
    /// här" - visible on the day, not silent.
    /// </summary>
    Lapsed = 2
}
