namespace Hemordna.Domain.Reminders;

/// <summary>
/// WHO, among the household, can see a <see cref="Reminder"/>'s time when
/// <see cref="Reminder.Visibility"/> is not <see cref="ReminderVisibility.Private"/> - a
/// genuinely independent question from <see cref="ReminderVisibility"/>'s WHAT. Every level
/// applies equally to whoever the audience turns out to be - there is no per-person level (see
/// <see cref="Reminder.SetAudience"/>). Always chosen by the owner, same as
/// <see cref="ReminderVisibility"/>.
/// </summary>
public enum ReminderAudience
{
    /// <summary>
    /// Default. Every current and future household member sees it - nobody has to go back and
    /// re-share when a new member joins. <see cref="Reminder.Shares"/> is always empty here; see
    /// <see cref="Reminder.SetAudience"/>.
    /// </summary>
    Everyone = 0,

    /// <summary>
    /// Only the members recorded in <see cref="Reminder.Shares"/> see it. An empty
    /// <see cref="Reminder.Shares"/> is a valid state where nobody does - deliberately never
    /// read as "everyone". This is its own explicit value rather than inferring "selected" from
    /// a non-empty list, precisely so that a lost or missing share row can only ever narrow who
    /// sees the time, never widen it (docs/ARCHITECTURE.md, "Beslut: Synlighet för
    /// påminnelser").
    /// </summary>
    Selected = 1
}
