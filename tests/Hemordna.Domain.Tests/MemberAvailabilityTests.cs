using Hemordna.Domain.Common;
using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class MemberAvailabilityTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    // 2026-02-06 is a Friday.
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private static readonly WeeklyTimeBudget WeekdayBudget = WeeklyTimeBudget.Create(
        new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 30,
            [DayOfWeek.Tuesday] = 45,
            [DayOfWeek.Wednesday] = 20,
            [DayOfWeek.Thursday] = 30,
            [DayOfWeek.Friday] = 20,
            [DayOfWeek.Saturday] = 60,
            [DayOfWeek.Sunday] = 0
        });

    private static HouseholdMember CreateMember()
    {
        var household = Household.Create("Familjen", CreatedAt);
        return household.AddMember("Anna", WeekdayBudget, CreatedAt);
    }

    [Fact]
    public void Without_an_override_the_weekly_budget_applies()
    {
        var member = CreateMember();

        Assert.Equal(20, member.AvailableMinutesOn(Friday, availabilityOverride: null));
    }

    [Fact]
    public void An_override_replaces_the_weekly_budget_for_that_date_only()
    {
        var member = CreateMember();
        var lessTimeToday = MemberAvailability.Create(member.HouseholdId, member.Id, Friday, 5);

        Assert.Equal(5, member.AvailableMinutesOn(Friday, lessTimeToday));

        // The normal week survives the one-off change.
        Assert.Equal(20, member.AvailableMinutesOn(Friday, availabilityOverride: null));
        Assert.Equal(60, member.AvailableMinutesOn(Friday.AddDays(1), availabilityOverride: null));
    }

    [Fact]
    public void An_override_of_zero_minutes_means_no_time_today()
    {
        var member = CreateMember();
        var noTimeToday = MemberAvailability.Create(member.HouseholdId, member.Id, Friday, 0);

        Assert.Equal(0, member.AvailableMinutesOn(Friday, noTimeToday));
    }

    [Fact]
    public void An_override_belonging_to_another_member_is_rejected()
    {
        var member = CreateMember();
        var otherMembersOverride = MemberAvailability.Create(member.HouseholdId, Guid.NewGuid(), Friday, 5);

        Assert.Throws<DomainException>(() => member.AvailableMinutesOn(Friday, otherMembersOverride));
    }

    [Fact]
    public void An_override_for_another_date_is_rejected()
    {
        var member = CreateMember();
        var yesterdaysOverride = MemberAvailability.Create(
            member.HouseholdId, member.Id, Friday.AddDays(-1), 5);

        Assert.Throws<DomainException>(() => member.AvailableMinutesOn(Friday, yesterdaysOverride));
    }

    [Fact]
    public void Negative_available_minutes_are_rejected()
    {
        var member = CreateMember();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemberAvailability.Create(member.HouseholdId, member.Id, Friday, -1));

        var availability = MemberAvailability.Create(member.HouseholdId, member.Id, Friday, 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => availability.ChangeAvailableMinutes(-1));
    }
}
