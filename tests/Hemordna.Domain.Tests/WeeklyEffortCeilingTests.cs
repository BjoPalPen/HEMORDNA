using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class WeeklyEffortCeilingTests
{
    [Fact]
    public void Default_allows_heavy_every_weekday()
    {
        var ceiling = WeeklyEffortCeiling.Default;

        Assert.All(
            Enum.GetValues<DayOfWeek>(),
            day => Assert.Equal(TaskEffort.Heavy, ceiling.CeilingFor(day)));
    }

    [Fact]
    public void Create_defaults_unlisted_weekdays_to_heavy()
    {
        var ceiling = WeeklyEffortCeiling.Create(new Dictionary<DayOfWeek, TaskEffort>
        {
            [DayOfWeek.Monday] = TaskEffort.Light
        });

        Assert.Equal(TaskEffort.Light, ceiling.CeilingFor(DayOfWeek.Monday));
        Assert.Equal(TaskEffort.Heavy, ceiling.CeilingFor(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Uniform_applies_the_same_ceiling_every_weekday()
    {
        var ceiling = WeeklyEffortCeiling.Uniform(TaskEffort.Medium);

        Assert.All(
            Enum.GetValues<DayOfWeek>(),
            day => Assert.Equal(TaskEffort.Medium, ceiling.CeilingFor(day)));
    }

    [Fact]
    public void WithDay_leaves_the_original_ceiling_untouched()
    {
        var original = WeeklyEffortCeiling.Uniform(TaskEffort.Heavy);

        var changed = original.WithDay(DayOfWeek.Monday, TaskEffort.Light);

        Assert.Equal(TaskEffort.Heavy, original.CeilingFor(DayOfWeek.Monday));
        Assert.Equal(TaskEffort.Light, changed.CeilingFor(DayOfWeek.Monday));
    }

    [Theory]
    [InlineData(TaskEffort.Light, TaskEffort.Light, true)]
    [InlineData(TaskEffort.Medium, TaskEffort.Light, false)]
    [InlineData(TaskEffort.Light, TaskEffort.Heavy, true)]
    [InlineData(TaskEffort.Heavy, TaskEffort.Heavy, true)]
    public void Allows_checks_the_task_against_the_days_ceiling(TaskEffort taskEffort, TaskEffort dayCeiling, bool expected)
    {
        var ceiling = WeeklyEffortCeiling.Uniform(dayCeiling);

        Assert.Equal(expected, ceiling.Allows(taskEffort, DayOfWeek.Monday));
    }

    [Fact]
    public void Two_ceilings_with_the_same_weekdays_are_equal()
    {
        var left = WeeklyEffortCeiling.Default.WithDay(DayOfWeek.Friday, TaskEffort.Light);
        var right = WeeklyEffortCeiling.Create(new Dictionary<DayOfWeek, TaskEffort> { [DayOfWeek.Friday] = TaskEffort.Light });

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
