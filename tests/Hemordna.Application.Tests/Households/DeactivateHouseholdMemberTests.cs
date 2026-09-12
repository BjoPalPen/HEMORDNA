using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class DeactivateHouseholdMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private DeactivateHouseholdMember CreateUseCase() => new(_households);

    /// <summary>A household with just its creator - who, since CreateHousehold, is also its
    /// first (and here, only) manager. See docs/ARCHITECTURE.md "Beslut: Vem får ändra vad".</summary>
    private async Task<(Guid HouseholdId, HouseholdMember Creator)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Deactivates_a_member_who_is_not_the_households_last_manager()
    {
        var (householdId, creator) = await ArrangeHouseholdAsync();
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        var other = household!.AddMember("Björn", WeeklyTimeBudget.Empty, Now);
        await _households.UpdateAsync(household, CancellationToken.None);

        var deactivated = await CreateUseCase().HandleAsync(householdId, other.Id, CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);
        Assert.False(other.IsActive);
        Assert.True(creator.IsActive);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var deactivated = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(deactivated);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var deactivated = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(deactivated);
    }

    [Fact]
    public async Task Cannot_deactivate_the_households_last_active_manager()
    {
        // The creator is the household's only member and its only manager - deactivating them
        // would leave the household permanently unable to manage itself (no support channel,
        // one household per user forever - see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad").
        var (householdId, creator) = await ArrangeHouseholdAsync();

        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => CreateUseCase().HandleAsync(householdId, creator.Id, CancellationToken.None));

        Assert.True(creator.IsActive);
    }
}
