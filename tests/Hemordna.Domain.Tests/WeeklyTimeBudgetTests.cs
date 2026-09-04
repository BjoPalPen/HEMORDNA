using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class WeeklyTimeBudgetTests
{
    [Fact]
    public void Empty_allocates_no_time_on_any_weekday()
    {
        var budget = WeeklyTimeBudget.Empty;

        Assert.All(
            Enum.GetValues<DayOfWeek>(),
            day => Assert.Equal(0, budget.MinutesFor(day)));
        Assert.Equal(0, budget.TotalWeeklyMinutes);
    }

    [Fact]
    public void Create_defaults_unlisted_weekdays_to_zero()
    {
        var budget = WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 30,
            [DayOfWeek.Saturday] = 60
        });

        Assert.Equal(30, budget.MinutesFor(DayOfWeek.Monday));
        Assert.Equal(60, budget.MinutesFor(DayOfWeek.Saturday));
        Assert.Equal(0, budget.MinutesFor(DayOfWeek.Sunday));
        Assert.Equal(90, budget.TotalWeeklyMinutes);
    }

    [Fact]
    public void Uniform_allocates_the_same_time_every_weekday()
    {
        var budget = WeeklyTimeBudget.Uniform(15);

        Assert.Equal(105, budget.TotalWeeklyMinutes);
    }

    [Fact]
    public void WithDay_leaves_the_original_budget_untouched()
    {
        var original = WeeklyTimeBudget.Uniform(30);

        var changed = original.WithDay(DayOfWeek.Sunday, 0);

        Assert.Equal(30, original.MinutesFor(DayOfWeek.Sunday));
        Assert.Equal(0, changed.MinutesFor(DayOfWeek.Sunday));
    }

    [Fact]
    public void Negative_minutes_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WeeklyTimeBudget.Uniform(-1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeeklyTimeBudget.Empty.WithDay(DayOfWeek.Monday, -5));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = -1 }));
    }

    [Fact]
    public void Two_budgets_with_the_same_weekdays_are_equal()
    {
        var left = WeeklyTimeBudget.Empty.WithDay(DayOfWeek.Friday, 20);
        var right = WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int> { [DayOfWeek.Friday] = 20 });

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
