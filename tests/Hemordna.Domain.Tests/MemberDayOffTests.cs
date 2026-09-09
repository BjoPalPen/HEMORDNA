using Hemordna.Domain.Common;
using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class MemberDayOffTests
{
    // 2026-03-02 is a Monday.
    private static readonly DateOnly Today = new(2026, 3, 2);

    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    [Fact]
    public void A_date_in_the_past_is_rejected()
    {
        Assert.Throws<DomainException>(
            () => MemberDayOff.Create(HouseholdId, MemberId, Today.AddDays(-1), Today));
    }

    [Fact]
    public void Today_itself_is_a_valid_day_off()
    {
        var dayOff = MemberDayOff.Create(HouseholdId, MemberId, Today, Today);

        Assert.Equal(Today, dayOff.Date);
    }

    [Fact]
    public void Exactly_seven_days_ahead_is_the_furthest_allowed()
    {
        var dayOff = MemberDayOff.Create(HouseholdId, MemberId, Today.AddDays(7), Today);

        Assert.Equal(Today.AddDays(7), dayOff.Date);
    }

    [Fact]
    public void Eight_days_ahead_is_rejected()
    {
        Assert.Throws<DomainException>(
            () => MemberDayOff.Create(HouseholdId, MemberId, Today.AddDays(8), Today));
    }
}
