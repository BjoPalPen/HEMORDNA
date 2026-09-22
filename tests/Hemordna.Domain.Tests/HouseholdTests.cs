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
    public void AddArea_allows_the_same_name_on_different_floors()
    {
        // The old household-wide uniqueness check would have rejected this - two rooms named
        // "Hall" on different floors is an intentional, already-supported case (see
        // docs/ARCHITECTURE.md "Två olika rum med samma namn på olika våningar").
        var household = Household.Create("Familjen", CreatedAt);

        var first = household.AddArea("Hall", floor: "Våning 1");
        var second = household.AddArea("Hall", floor: "Våning 2");

        Assert.Equal("Våning 1", first.Floor);
        Assert.Equal("Våning 2", second.Floor);
        Assert.Equal(2, household.Areas.Count);
    }

    [Fact]
    public void AddArea_rejects_a_duplicate_name_on_the_same_floor_including_no_floor()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddArea("Hall");

        Assert.Throws<DomainException>(() => household.AddArea("HALL"));
    }

    [Fact]
    public void SetAreaFloor_moves_an_area_to_a_new_floor()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var area = household.AddArea("Kok", floor: "Våning 1");

        household.SetAreaFloor(area.Id, "Våning 2");

        Assert.Equal("Våning 2", area.Floor);
    }

    [Fact]
    public void SetAreaFloor_rejects_moving_into_a_floor_where_the_name_is_already_taken()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddArea("Hall", floor: "Våning 2");
        var area = household.AddArea("Hall", floor: "Våning 1");

        Assert.Throws<DomainException>(() => household.SetAreaFloor(area.Id, "Våning 2"));
        Assert.Equal("Våning 1", area.Floor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AddArea_stores_a_blank_floor_as_null(string? floor)
    {
        var household = Household.Create("Familjen", CreatedAt);

        var area = household.AddArea("Hall", floor: floor);

        Assert.Null(area.Floor);
    }

    [Fact]
    public void A_new_area_is_not_paused()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var area = household.AddArea("Badrum");

        Assert.Null(area.PausedUntil);
        Assert.False(area.IsPausedOn(DateOnly.FromDateTime(CreatedAt.Date)));
    }

    [Fact]
    public void Area_pause_is_paused_through_and_including_the_given_date()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var area = household.AddArea("Badrum");
        var until = new DateOnly(2026, 3, 10);

        area.Pause(until);

        Assert.True(area.IsPausedOn(until));
        Assert.True(area.IsPausedOn(until.AddDays(-1)));
        Assert.False(area.IsPausedOn(until.AddDays(1)));
    }

    [Fact]
    public void Area_resume_lifts_a_pause_immediately()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var area = household.AddArea("Badrum");
        var until = new DateOnly(2026, 3, 10);
        area.Pause(until);

        area.Resume();

        Assert.Null(area.PausedUntil);
        Assert.False(area.IsPausedOn(until));
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

    [Fact]
    public void A_new_household_is_not_paused()
    {
        var household = Household.Create("Familjen", CreatedAt);

        Assert.Null(household.PausedUntil);
        Assert.False(household.IsPausedOn(DateOnly.FromDateTime(CreatedAt.Date)));
    }

    [Fact]
    public void Pause_is_paused_through_and_including_the_given_date()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var until = new DateOnly(2026, 3, 10);

        household.Pause(until);

        Assert.True(household.IsPausedOn(until));
        Assert.True(household.IsPausedOn(until.AddDays(-1)));
        Assert.False(household.IsPausedOn(until.AddDays(1)));
    }

    [Fact]
    public void Resume_lifts_a_pause_immediately()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var until = new DateOnly(2026, 3, 10);
        household.Pause(until);

        household.Resume();

        Assert.Null(household.PausedUntil);
        Assert.False(household.IsPausedOn(until));
    }

    [Fact]
    public void SetMemberCanManageHousehold_grants_it_to_a_member_with_an_account()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        member.LinkToUser(Guid.NewGuid());

        household.SetMemberCanManageHousehold(member.Id, true);

        Assert.True(member.CanManageHousehold);
    }

    [Fact]
    public void SetMemberCanManageHousehold_rejects_an_unknown_member()
    {
        var household = Household.Create("Familjen", CreatedAt);

        Assert.Throws<DomainException>(() => household.SetMemberCanManageHousehold(Guid.NewGuid(), true));
    }

    [Fact]
    public void SetMemberCanManageHousehold_cannot_remove_it_from_the_last_active_manager()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        member.LinkToUser(Guid.NewGuid());
        household.SetMemberCanManageHousehold(member.Id, true);

        Assert.Throws<DomainException>(() => household.SetMemberCanManageHousehold(member.Id, false));
        Assert.True(member.CanManageHousehold);
    }

    [Fact]
    public void SetMemberCanManageHousehold_can_remove_it_when_another_active_member_also_has_it()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var first = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        first.LinkToUser(Guid.NewGuid());
        var second = household.AddMember("Björn", WeeklyTimeBudget.Empty, CreatedAt);
        second.LinkToUser(Guid.NewGuid());
        household.SetMemberCanManageHousehold(first.Id, true);
        household.SetMemberCanManageHousehold(second.Id, true);

        household.SetMemberCanManageHousehold(first.Id, false);

        Assert.False(first.CanManageHousehold);
        Assert.True(second.CanManageHousehold);
    }

    [Fact]
    public void DeactivateMember_cannot_deactivate_the_last_active_manager()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        member.LinkToUser(Guid.NewGuid());
        household.SetMemberCanManageHousehold(member.Id, true);

        Assert.Throws<DomainException>(() => household.DeactivateMember(member.Id));
        Assert.True(member.IsActive);
    }

    [Fact]
    public void DeactivateMember_can_deactivate_a_manager_when_another_active_member_also_manages()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var first = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        first.LinkToUser(Guid.NewGuid());
        var second = household.AddMember("Björn", WeeklyTimeBudget.Empty, CreatedAt);
        second.LinkToUser(Guid.NewGuid());
        household.SetMemberCanManageHousehold(first.Id, true);
        household.SetMemberCanManageHousehold(second.Id, true);

        household.DeactivateMember(first.Id);

        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
    }

    [Fact]
    public void DeactivateMember_can_always_deactivate_a_member_who_does_not_manage_the_household()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var manager = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        manager.LinkToUser(Guid.NewGuid());
        household.SetMemberCanManageHousehold(manager.Id, true);
        var other = household.AddMember("Björn", WeeklyTimeBudget.Empty, CreatedAt);

        household.DeactivateMember(other.Id);

        Assert.False(other.IsActive);
    }

    [Fact]
    public void DeactivateMember_rejects_an_unknown_member()
    {
        var household = Household.Create("Familjen", CreatedAt);

        Assert.Throws<DomainException>(() => household.DeactivateMember(Guid.NewGuid()));
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
