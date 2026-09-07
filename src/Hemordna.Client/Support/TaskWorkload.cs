using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// Turns a household's task definitions into an estimate of real, ongoing weekly workload -
/// unlike a flat sum of every task's own <see cref="TaskDefinitionResponse.EstimatedMinutes"/>
/// (see Omraden.razor's "Totalt: X uppgifter · Y min"), which weighs a daily chore the same as
/// a monthly one even though the daily one demands roughly 30 times as much time over a month.
/// Planning-stage only - see PRODUCT.md §4/§8 for why day-to-day task UI never shows a minute
/// count instead of a qualitative level; this is the same kind of overview exception already
/// made for the flat total.
/// </summary>
public static class TaskWorkload
{
    private const double AverageDaysPerMonth = 30.44;

    /// <summary>
    /// This task's estimated share of an average week, given how often it actually repeats -
    /// zero for a task with neither a <see cref="RecurrenceRuleContract"/> nor a
    /// <see cref="TaskDefinitionResponse.StaleAfterDays"/> interval (a one-off, manually
    /// scheduled task has no ongoing cadence to project forward).
    /// </summary>
    public static double WeeklyMinutes(TaskDefinitionResponse task)
    {
        if (task.Recurrence is { } recurrence)
        {
            var occurrencesPerWeek = recurrence.Frequency switch
            {
                "Daily" => 7.0 / recurrence.Interval,
                "Weekly" => 1.0 / recurrence.Interval,
                // Regardless of whether it is anchored to a day-of-month or an "nth weekday"
                // (RecurrenceRuleContract.MonthlyWeek), a monthly rule falls due about once
                // every Interval months either way.
                "Monthly" => 7.0 / (AverageDaysPerMonth * recurrence.Interval),
                _ => 0
            };

            return task.EstimatedMinutes * occurrencesPerWeek;
        }

        if (task.StaleAfterDays is { } staleAfterDays && staleAfterDays > 0)
        {
            return task.EstimatedMinutes * (7.0 / staleAfterDays);
        }

        return 0;
    }

    /// <summary>Rounded total across every given (already-filtered-to-active) task.</summary>
    public static int TotalWeeklyMinutes(IEnumerable<TaskDefinitionResponse> tasks)
        => (int)Math.Round(tasks.Sum(WeeklyMinutes));
}
