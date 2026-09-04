using Hemordna.Domain.Common;
using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class HouseholdTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_the_name()
    {
        var household = Household.Create("  Familjen  ", CreatedAt);

        Assert.Equal("Familjen", household.Name);
        Assert.Equal(CreatedAt, household.CreatedAt);
        Assert.NotEqual(Guid.Empty, household.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_name(string? name)
        => Assert.Throws<ArgumentException>(() => Household.Create(name!, CreatedAt));

    [Fact]
    public void AddMember_scopes_the_member_to_the_household()
    {
        var household = Household.Create("Familjen", CreatedAt);

        var member = household.AddMember("Anna", WeeklyTimeBudget.Uniform(30), CreatedAt);

        Assert.Equal(household.Id, member.HouseholdId);
        Assert.True(member.IsActive);
        Assert.Equal(member, Assert.Single(household.Members));
    }

    [Fact]
    public void AddMember_rejects_a_duplicate_name_regardless_of_casing()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);

        Assert.Throws<DomainException>(
            () => household.AddMember(" anna ", WeeklyTimeBudget.Empty, CreatedAt));
    }

    [Fact]
    public void AddArea_scopes_the_area_to_the_household()
    {
        var household = Household.Create("Familjen", CreatedAt);

        var area = household.AddArea("Kok");

        Assert.Equal(household.Id, area.HouseholdId);
        Assert.True(area.IsActive);
        Assert.Equal(area, Assert.Single(household.Areas));
    }

    [Fact]
    public void AddArea_rejects_a_duplicate_name_regardless_of_casing()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddArea("Badrum");

        Assert.Throws<DomainException>(() => household.AddArea("BADRUM"));
    }

    [Fact]
    public void A_household_can_hold_a_single_member()
    {
        var household = Household.Create("Ensam", CreatedAt);

        household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(20), CreatedAt);

        Assert.Single(household.Members);
    }
}
