using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class VisitKindClassifierTests
{
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public void A_daily_task_with_interval_one_is_routine()
    {
        var recurrence = RecurrenceRule.Daily(Monday);

        Assert.Equal(VisitKind.Routine, VisitKindClassifier.Of(recurrence, TaskEffort.Light));
    }

    [Fact]
    public void Routine_wins_over_effort_even_when_the_task_is_heavy()
    {
        // A daily task's interval-1 status decides Routine before effort is even considered -
        // e.g. a household that (unusually) marks a daily chore as Heavy still gets Routine's
        // "never claims the room" treatment, not DeepClean's.
        var recurrence = RecurrenceRule.Daily(Monday);

        Assert.Equal(VisitKind.Routine, VisitKindClassifier.Of(recurrence, TaskEffort.Heavy));
    }

    [Fact]
    public void A_daily_task_with_a_wider_interval_is_not_routine()
    {
        var recurrence = RecurrenceRule.Daily(Monday, everyNDays: 2);

        Assert.NotEqual(VisitKind.Routine, VisitKindClassifier.Of(recurrence, TaskEffort.Light));
    }

    [Fact]
    public void A_heavy_non_routine_task_is_deep_clean()
    {
        var recurrence = RecurrenceRule.Weekly(Monday, DayOfWeek.Monday);

        Assert.Equal(VisitKind.DeepClean, VisitKindClassifier.Of(recurrence, TaskEffort.Heavy));
    }

    [Theory]
    [InlineData(TaskEffort.Light)]
    [InlineData(TaskEffort.Medium)]
    public void A_non_heavy_weekly_task_is_regular_clean(TaskEffort effort)
    {
        var recurrence = RecurrenceRule.Weekly(Monday, DayOfWeek.Monday);

        Assert.Equal(VisitKind.RegularClean, VisitKindClassifier.Of(recurrence, effort));
    }

    [Fact]
    public void A_monthly_task_is_regular_clean_unless_heavy()
    {
        var recurrence = RecurrenceRule.Monthly(Monday);

        Assert.Equal(VisitKind.RegularClean, VisitKindClassifier.Of(recurrence, TaskEffort.Medium));
        Assert.Equal(VisitKind.DeepClean, VisitKindClassifier.Of(recurrence, TaskEffort.Heavy));
    }

    [Fact]
    public void An_as_needed_task_with_no_recurrence_is_regular_clean_unless_heavy()
    {
        Assert.Equal(VisitKind.RegularClean, VisitKindClassifier.Of(recurrence: null, TaskEffort.Medium));
        Assert.Equal(VisitKind.DeepClean, VisitKindClassifier.Of(recurrence: null, TaskEffort.Heavy));
    }

    [Fact]
    public void Of_TaskDefinition_reads_the_definitions_own_recurrence_and_effort()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Skrubba dusch", 15, DateTimeOffset.UnixEpoch);
        definition.ChangeEffort(TaskEffort.Heavy);
        definition.SetRecurrence(RecurrenceRule.Weekly(Monday, DayOfWeek.Monday));

        Assert.Equal(VisitKind.DeepClean, VisitKindClassifier.Of(definition));
    }
}
