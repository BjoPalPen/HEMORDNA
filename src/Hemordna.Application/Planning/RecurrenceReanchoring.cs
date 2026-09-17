using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// The single place that knows how to move a Weekly/Monthly <see cref="RecurrenceRule"/> to a
/// new weekday without ever colliding with, duplicating, or leaving a gap before, an
/// already-generated occurrence. Shared by <see cref="ApplyWeeklyPlan"/> (moving a whole visit)
/// and <c>SetTaskPreferredWeekday</c> (locking a single task) - Björn's explicit instruction was
/// to reuse this technique rather than write a second version of it.
/// </summary>
/// <remarks>
/// <b>No duplicate, no skipped period.</b> The new rule is always anchored from <c>today</c>
/// forward (via <see cref="RecurrenceRule.Weekly"/> or <see cref="RecurrenceRule.MonthlyOnWeekday"/>,
/// both of which normalise their own anchor to the first matching date ON OR AFTER the date
/// passed in) - never from the OLD cursor an occurrence generator reads. Because occurrences are
/// never generated ahead of today in the first place, no occurrence can ever exist for a date
/// later than today - so an anchor rooted at today can never collide with, duplicate, or skip,
/// anything already on the calendar.
/// </remarks>
internal static class RecurrenceReanchoring
{
    /// <summary>
    /// The re-anchored rule for <paramref name="weekday"/>, or <c>null</c> when
    /// <paramref name="current"/> is neither Weekly nor Monthly (nothing to re-anchor).
    /// <paramref name="monthlyWeek"/> is only evaluated for a Monthly rule - callers that derive
    /// it from something stateful (e.g. a rotation counter shared across many tasks) can rely on
    /// it never running for a Weekly one.
    /// </summary>
    public static RecurrenceRule? ForWeekday(
        RecurrenceRule current, DateOnly today, DayOfWeek weekday, Func<WeekOfMonth> monthlyWeek)
        => current.Frequency switch
        {
            RecurrenceFrequency.Weekly => RecurrenceRule.Weekly(today, weekday, current.Interval),
            RecurrenceFrequency.Monthly => RecurrenceRule.MonthlyOnWeekday(today, monthlyWeek(), weekday, current.Interval),
            _ => null
        };

    /// <summary>
    /// True when <paramref name="newRule"/> actually differs from <paramref name="current"/> in
    /// what a placement decision controls - weekday, week-of-month and interval - never
    /// <c>StartDate</c>, which is always freshly normalised from today and so would differ on
    /// almost every call even when nothing meaningful changed.
    /// </summary>
    public static bool HasMeaningfulChange(RecurrenceRule? newRule, RecurrenceRule current)
        => newRule is not null
            && !(newRule.Weekday == current.Weekday
                && newRule.MonthlyWeek == current.MonthlyWeek
                && newRule.Interval == current.Interval);

    /// <summary>
    /// Locks <paramref name="definition"/> to <paramref name="weekday"/> and re-anchors its
    /// <see cref="TaskDefinition.Recurrence"/> to match, using the exact same "no duplicate, no
    /// skipped period" technique as everywhere else in this file. Shared by
    /// <c>SetTaskPreferredWeekday</c> (locking a single task by hand) and <c>ApplyWeeklyPlan</c>
    /// (Björns beslut: "ett flyttat besök låses" - moving a visit in the weekly plan locks every
    /// task in it exactly the same way a manual lock does) - one place for the combined
    /// "set the requirement, then re-anchor if needed" sequence, instead of two copies of it.
    /// </summary>
    public static void LockToWeekday(TaskDefinition definition, DateOnly today, DayOfWeek weekday)
    {
        // Validates weekday/frequency compatibility - throws before anything else changes.
        definition.SetPreferredWeekday(weekday);

        if (definition.Recurrence is { } current)
        {
            var reanchored = ForWeekday(current, today, weekday, () => current.MonthlyWeek ?? WeekOfMonth.First);

            if (HasMeaningfulChange(reanchored, current))
            {
                definition.SetRecurrence(reanchored);
            }
        }
    }
}
