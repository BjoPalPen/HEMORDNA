using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class CreateHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 9, 15, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private CreateHousehold CreateUseCase() => new(_households, new FixedTimeProvider(Now));

    [Fact]
    public async Task Creates_and_persists_the_household()
    {
        var household = await CreateUseCase().HandleAsync("Familjen", CancellationToken.None);

        Assert.Equal("Familjen", household.Name);
        Assert.NotEqual(Guid.Empty, household.Id);
        Assert.Equal(1, _households.AddCallCount);

        var persisted = await _households.FindByIdAsync(household.Id, CancellationToken.None);
        Assert.Same(household, persisted);
    }

    [Fact]
    public async Task Stamps_the_household_with_the_injected_clock()
    {
        var household = await CreateUseCase().HandleAsync("Familjen", CancellationToken.None);

        Assert.Equal(Now, household.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_a_blank_name_without_persisting_anything(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateUseCase().HandleAsync(name, CancellationToken.None));

        Assert.Equal(0, _households.AddCallCount);
    }

    [Fact]
    public async Task Two_households_may_share_a_name()
    {
        // Household names are not a system-wide unique key - plenty of homes are "Familjen".
        var useCase = CreateUseCase();

        var first = await useCase.HandleAsync("Familjen", CancellationToken.None);
        var second = await useCase.HandleAsync("Familjen", CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
    }
}
