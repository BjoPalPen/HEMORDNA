using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
namespace Hemordna.Domain.Tests;
public class PlanningInputSecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static TaskDefinition Definition(int minutes = 20)
        => TaskDefinition.Create(Guid.NewGuid(), "Diska", minutes, Now);
    [Theory]
    [InlineData(1441)]
    [InlineData(int.MaxValue)]
    public void Extreme_estimates_are_rejected_on_create_and_update(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Definition(minutes));
        var definition = Definition();
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ChangeEstimatedMinutes(minutes));
        Assert.Equal(20, definition.EstimatedMinutes);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1440)]
    public void Supported_estimates_remain_valid(int minutes)
    {
        var definition = Definition(minutes);
        definition.ChangeEstimatedMinutes(minutes);
        Assert.Equal(minutes, definition.ScheduleFor(new DateOnly(2026, 3, 2), Now).EstimatedMinutes);
    }
    [Fact]
    public void Legacy_large_credits_use_the_net_sum_before_clamping()
    {
        var household = Guid.NewGuid();
        var member = Guid.NewGuid();
        var date = new DateOnly(2026, 3, 2);
        var earned = MemberTimeCredit.Earned(household, member, date, TimeCreditReason.WorkedAhead, int.MaxValue, Guid.NewGuid());
        var spent = MemberTimeCredit.Consumed(household, member, date, TimeCreditReason.RotationSkipped, int.MaxValue, Guid.NewGuid());
        Assert.Equal(300, MemberTimeCredit.BalanceOf([earned, earned], 300));
        Assert.Equal(0, MemberTimeCredit.BalanceOf([spent, spent], 300));
        Assert.Equal(0, MemberTimeCredit.BalanceOf([earned, earned, spent, spent], 300));
    }
    [Fact]
    public void Scheduling_accepts_the_horizon_and_rejects_its_next_day_and_calendar_extremes()
    {
        var definition = Definition();
        var maximum = new DateOnly(2031, 3, 2);
        Assert.Equal(maximum, definition.ScheduleFor(maximum, Now).ScheduledDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ScheduleFor(maximum.AddDays(1), Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ScheduleFor(DateOnly.MaxValue, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ScheduleFor(DateOnly.MinValue, Now));
    }
    [Fact]
    public void Old_backlog_can_be_moved_to_the_present_but_not_calendar_extremes()
    {
        var old = new DateTimeOffset(2020, 3, 2, 8, 0, 0, TimeSpan.Zero);
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Diska", 20, old);
        var deferred = definition.ScheduleFor(new DateOnly(2020, 3, 2), old);
        deferred.DeferTo(new DateOnly(2026, 3, 2));
        Assert.Equal(new DateOnly(2020, 3, 2), deferred.OriginalScheduledDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => deferred.DeferTo(DateOnly.MaxValue));
        var reanchored = definition.ScheduleFor(new DateOnly(2020, 3, 2), old);
        reanchored.ReanchorTo(new DateOnly(2026, 3, 2));
        Assert.Equal(new DateOnly(2026, 3, 2), reanchored.OriginalScheduledDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => reanchored.ReanchorTo(DateOnly.MaxValue));
    }
}