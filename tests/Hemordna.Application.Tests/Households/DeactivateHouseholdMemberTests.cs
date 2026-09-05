using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class DeactivateHouseholdMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private DeactivateHouseholdMember CreateUseCase() => new(_households);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Deactivates_the_member()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var deactivated = await CreateUseCase().HandleAsync(householdId, member.Id, CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);
        Assert.False(member.IsActive);
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
}
