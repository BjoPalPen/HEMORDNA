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

    [Fact]
    public void Create_issues_an_eight_character_invite_code()
    {
        var household = Household.Create("Familjen", CreatedAt);

        Assert.Equal(8, household.InviteCode.Length);
    }

    [Fact]
    public void The_invite_code_only_uses_unambiguous_characters()
    {
        // No 0/O or 1/I/L - the code is meant to be read aloud or typed by hand.
        var household = Household.Create("Familjen", CreatedAt);

        Assert.Matches("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{8}$", household.InviteCode);
    }

    [Fact]
    public void Two_households_get_different_invite_codes()
    {
        var first = Household.Create("Familjen ett", CreatedAt);
        var second = Household.Create("Familjen tva", CreatedAt);

        Assert.NotEqual(first.InviteCode, second.InviteCode);
    }

    [Fact]
    public void RegenerateInviteCode_replaces_the_code()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var original = household.InviteCode;

        household.RegenerateInviteCode();

        Assert.NotEqual(original, household.InviteCode);
        Assert.Equal(8, household.InviteCode.Length);
    }

    [Fact]
    public void RegenerateInviteCode_does_not_affect_existing_members()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);

        household.RegenerateInviteCode();

        Assert.Single(household.Members);
    }
}

public class HouseholdMemberUserLinkTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private static HouseholdMember CreateMember()
        => Household.Create("Familjen", CreatedAt)
            .AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);

    [Fact]
    public void A_new_member_has_no_user()
    {
        // Members can be added by someone else - a child, a partner who has not signed up yet.
        Assert.Null(CreateMember().UserId);
    }

    [Fact]
    public void LinkToUser_records_the_user()
    {
        var member = CreateMember();
        var userId = Guid.NewGuid();

        member.LinkToUser(userId);

        Assert.Equal(userId, member.UserId);
    }

    [Fact]
    public void Linking_the_same_user_twice_is_a_no_op()
    {
        var member = CreateMember();
        var userId = Guid.NewGuid();

        member.LinkToUser(userId);
        member.LinkToUser(userId);

        Assert.Equal(userId, member.UserId);
    }

    [Fact]
    public void A_member_cannot_be_moved_to_a_different_user()
    {
        // Re-pointing a member would silently transfer their completion history.
        var member = CreateMember();
        member.LinkToUser(Guid.NewGuid());

        Assert.Throws<DomainException>(() => member.LinkToUser(Guid.NewGuid()));
    }

    [Fact]
    public void LinkToUser_rejects_an_empty_user_id()
        => Assert.Throws<ArgumentException>(() => CreateMember().LinkToUser(Guid.Empty));
}
