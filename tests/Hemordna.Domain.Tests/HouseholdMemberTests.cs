using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class HouseholdMemberTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private static HouseholdMember CreateMember()
        => Household.Create("Familjen", CreatedAt)
            .AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);

    [Fact]
    public void A_new_member_is_not_paused()
    {
        var member = CreateMember();

        Assert.Null(member.PausedUntil);
        Assert.False(member.IsPausedOn(DateOnly.FromDateTime(CreatedAt.Date)));
    }

    [Fact]
    public void Pause_is_paused_through_and_including_the_given_date()
    {
        var member = CreateMember();
        var until = new DateOnly(2026, 3, 10);

        member.Pause(until);

        Assert.True(member.IsPausedOn(until));
        Assert.True(member.IsPausedOn(until.AddDays(-1)));
        Assert.False(member.IsPausedOn(until.AddDays(1)));
    }

    [Fact]
    public void Resume_lifts_a_pause_immediately()
    {
        var member = CreateMember();
        var until = new DateOnly(2026, 3, 10);
        member.Pause(until);

        member.Resume();

        Assert.Null(member.PausedUntil);
        Assert.False(member.IsPausedOn(until));
    }
}
