using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class SetMemberCanManageHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private SetMemberCanManageHousehold CreateUseCase() => new(_households);

    private async Task<(Guid HouseholdId, HouseholdMember Creator)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    private async Task<HouseholdMember> AddAccountHolderAsync(Guid householdId, string name)
    {
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        var member = household!.AddMember(name, WeeklyTimeBudget.Empty, Now);
        member.LinkToUser(Guid.NewGuid());
        await _households.UpdateAsync(household, CancellationToken.None);
        return member;
    }

    private async Task<HouseholdMember> AddAccountlessMemberAsync(Guid householdId, string name)
    {
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        var member = household!.AddMember(name, WeeklyTimeBudget.Empty, Now);
        await _households.UpdateAsync(household, CancellationToken.None);
        return member;
    }

    [Fact]
    public async Task Grants_the_ability_to_a_member_with_an_account()
    {
        var (householdId, creator) = await ArrangeHouseholdAsync();
        var bjorn = await AddAccountHolderAsync(householdId, "Björn");

        var updated = await CreateUseCase().HandleAsync(householdId, bjorn.Id, true, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated.CanManageHousehold);
        Assert.True(creator.CanManageHousehold);
    }

    [Fact]
    public async Task Removes_the_ability_when_another_active_member_still_has_it()
    {
        var (householdId, creator) = await ArrangeHouseholdAsync();
        var bjorn = await AddAccountHolderAsync(householdId, "Björn");
        await CreateUseCase().HandleAsync(householdId, bjorn.Id, true, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(householdId, creator.Id, false, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.False(creator.CanManageHousehold);
        Assert.True(bjorn.CanManageHousehold);
    }

    [Fact]
    public async Task Cannot_remove_the_ability_from_the_households_last_manager()
    {
        var (householdId, creator) = await ArrangeHouseholdAsync();

        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => CreateUseCase().HandleAsync(householdId, creator.Id, false, CancellationToken.None));

        Assert.True(creator.CanManageHousehold);
    }

    [Fact]
    public async Task Cannot_grant_the_ability_to_a_member_with_no_account()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();
        var child = await AddAccountlessMemberAsync(householdId, "Liten Björn");

        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => CreateUseCase().HandleAsync(householdId, child.Id, true, CancellationToken.None));

        Assert.False(child.CanManageHousehold);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var updated = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), true, CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var updated = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), true, CancellationToken.None);

        Assert.Null(updated);
    }
}
