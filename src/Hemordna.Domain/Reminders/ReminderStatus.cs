namespace Hemordna.Domain.Reminders;

/// <summary>The lifecycle of a <see cref="Reminder"/>.</summary>
public enum ReminderStatus
{
    /// <summary>Still ahead - has not been cancelled.</summary>
    Upcoming = 0,

    /// <summary>Called off by its owner. A cancelled reminder cannot be changed.</summary>
    Cancelled = 1,

    /// <summary>
    /// Its owner ticked this one off ("Bocka av" in the UI) - the appointment has been dealt
    /// with, whatever that meant for them. Named after the action the member took, not after an
    /// interpretation of what happened to the appointment: the app cannot know whether they
    /// attended, rescheduled or simply decided it no longer matters, and must not imply any of
    /// them (docs/PRODUCT.md §8).
    /// <para>
    /// Explicitly NOT <see cref="Hemordna.Domain.Tasks.TaskOccurrenceStatus.Completed"/>, and
    /// never read as one: a reminder is not household work, so checking one off must never reach
    /// a time budget, a "N av M klara" count or a <c>MemberTimeCredit</c> (PRODUCT.md §11).
    /// Having been to the dentist is not having done more than your share of the cleaning.
    /// </para>
    /// <para>
    /// Also distinct from a reminder's time simply passing unattended, which stays
    /// <see cref="Upcoming"/> and leaves the day without any marking at all - PRODUCT.md §11,
    /// "En passerad tid blir tyst". This status is the owner's own, deliberate tick, and it is
    /// visible on the day precisely because they chose it.
    /// </para>
    /// </summary>
    CheckedOff = 2
}
