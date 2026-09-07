using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class RenameAreaTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private RenameArea CreateUseCase() => new(_households);

    [Fact]
    public async Task Renames_the_area()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = household.AddArea("Sovrum 1");
        await _households.UpdateAsync(household, CancellationToken.None);

        var result = await CreateUseCase().HandleAsync(household.Id, area.Id, "Björns rum", CancellationToken.None);

        Assert.Equal("Björns rum", result!.Name);
    }

    [Fact]
    public async Task Returns_null_for_an_area_outside_the_household()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var result = await CreateUseCase().HandleAsync(household.Id, Guid.NewGuid(), "Nytt namn", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var result = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), "Nytt namn", CancellationToken.None);

        Assert.Null(result);
    }
}
