using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class PauseHouseholdTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Until = new(2026, 3, 10);

    private readonly InMemoryHouseholdRepository _households = new();

    private PauseHousehold CreateUseCase() => new(_households);

    [Fact]
    public async Task Pauses_the_household_through_the_given_date()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(household.Id, Until, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(Until, updated.PausedUntil);
        Assert.Equal(1, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Null_resumes_an_already_paused_household()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.Pause(Until);
        await _households.AddAsync(household, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(household.Id, null, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated.PausedUntil);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var updated = await CreateUseCase().HandleAsync(Guid.NewGuid(), Until, CancellationToken.None);

        Assert.Null(updated);
        Assert.Equal(0, _households.UpdateCallCount);
    }
}
