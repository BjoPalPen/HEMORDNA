using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class RegenerateInviteCodeTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private RegenerateInviteCode CreateUseCase() => new(_households);

    [Fact]
    public async Task Replaces_the_households_invite_code()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);
        var original = household.InviteCode;

        var updated = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.NotEqual(original, updated.InviteCode);
        Assert.Equal(1, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var updated = await CreateUseCase().HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(updated);
        Assert.Equal(0, _households.UpdateCallCount);
    }

    [Fact]
    public async Task The_old_code_no_longer_resolves_to_the_household()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);
        var original = household.InviteCode;

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Null(await _households.FindByInviteCodeAsync(original, CancellationToken.None));
    }
}
