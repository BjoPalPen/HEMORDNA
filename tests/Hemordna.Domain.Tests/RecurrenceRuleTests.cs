using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class RecurrenceRuleTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public void Daily_recurs_every_N_days_from_the_start_date()
    {
        var rule = RecurrenceRule.Daily(Monday, everyNDays: 3);

        Assert.Equal(Monday, rule.NextOnOrAfter(Monday));
        Assert.Equal(Monday.AddDays(3), rule.NextOnOrAfter(Monday.AddDays(1)));
        Assert.Equal(Monday.AddDays(3), rule.NextOnOrAfter(Monday.AddDays(3)));
        Assert.Equal(Monday.AddDays(6), rule.NextOnOrAfter(Monday.AddDays(4)));
    }

    [Fact]
    public void Weekly_only_falls_on_the_given_weekday()
    {
        // Started on a Monday, but the rule is for Thursdays.
        var rule = RecurrenceRule.Weekly(Monday, DayOfWeek.Thursday);

        var firstThursday = Monday.AddDays(3);
        Assert.Equal(firstThursday, rule.NextOnOrAfter(Monday));
        Assert.Equal(firstThursday, rule.NextOnOrAfter(firstThursday));
        Assert.Equal(firstThursday.AddDays(7), rule.NextOnOrAfter(firstThursday.AddDays(1)));
    }

    [Fact]
    public void Weekly_every_N_weeks_skips_the_weeks_between()
    {
        var rule = RecurrenceRule.Weekly(Monday, DayOfWeek.Monday, everyNWeeks: 2);

        Assert.Equal(Monday, rule.NextOnOrAfter(Monday));
        Assert.Equal(Monday.AddDays(14), rule.NextOnOrAfter(Monday.AddDays(1)));
        Assert.Equal(Monday.AddDays(14), rule.NextOnOrAfter(Monday.AddDays(7)));
    }

    [Fact]
    public void Monthly_recurs_on_the_same_day_of_month()
    {
        var startDate = new DateOnly(2026, 1, 31);
        var rule = RecurrenceRule.Monthly(startDate);

        // February has 28 days in 2026, so .NET clamps 31 -> 28.
        Assert.Equal(new DateOnly(2026, 2, 28), rule.NextOnOrAfter(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void Monthly_every_N_months_skips_the_months_between()
    {
        var startDate = new DateOnly(2026, 1, 15);
        var rule = RecurrenceRule.Monthly(startDate, everyNMonths: 3);

        Assert.Equal(new DateOnly(2026, 4, 15), rule.NextOnOrAfter(new DateOnly(2026, 2, 1)));
        Assert.Equal(new DateOnly(2026, 7, 15), rule.NextOnOrAfter(new DateOnly(2026, 4, 16)));
    }

    [Fact]
    public void MonthlyOnWeekday_finds_the_third_tuesday()
    {
        var rule = RecurrenceRule.MonthlyOnWeekday(new DateOnly(2026, 3, 1), WeekOfMonth.Third, DayOfWeek.Tuesday);

        // The third Tuesday of March 2026 is the 17th.
        Assert.Equal(new DateOnly(2026, 3, 17), rule.NextOnOrAfter(new DateOnly(2026, 3, 1)));
        // Asking after it in the same month rolls to the next month's third Tuesday (April 21).
        Assert.Equal(new DateOnly(2026, 4, 21), rule.NextOnOrAfter(new DateOnly(2026, 3, 18)));
    }

    [Fact]
    public void MonthlyOnWeekday_last_finds_the_final_occurrence_in_the_month()
    {
        var rule = RecurrenceRule.MonthlyOnWeekday(new DateOnly(2026, 3, 1), WeekOfMonth.Last, DayOfWeek.Friday);

        // The last Friday of March 2026 is the 27th.
        Assert.Equal(new DateOnly(2026, 3, 27), rule.NextOnOrAfter(new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Equality_is_by_value()
    {
        var a = RecurrenceRule.Weekly(Monday, DayOfWeek.Thursday);
        var b = RecurrenceRule.Weekly(Monday, DayOfWeek.Thursday);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_non_positive_interval(int interval)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RecurrenceRule.Daily(Monday, interval));
}
