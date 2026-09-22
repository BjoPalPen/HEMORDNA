using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class AddAreaTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private AddArea CreateUseCase() => new(_households);

    [Fact]
    public async Task Adds_the_area_with_no_floor_by_default()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = await CreateUseCase().HandleAsync(household.Id, "Kök", CancellationToken.None);

        Assert.Equal("Kök", area!.Name);
        Assert.Null(area.Floor);
    }

    [Fact]
    public async Task Adds_the_area_on_the_given_floor()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = await CreateUseCase().HandleAsync(household.Id, "Kök", CancellationToken.None, "Våning 1");

        Assert.Equal("Våning 1", area!.Floor);
    }

    [Fact]
    public async Task Allows_the_same_name_on_different_floors()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var useCase = CreateUseCase();

        var first = await useCase.HandleAsync(household.Id, "Hall", CancellationToken.None, "Våning 1");
        var second = await useCase.HandleAsync(household.Id, "Hall", CancellationToken.None, "Våning 2");

        Assert.Equal("Våning 1", first!.Floor);
        Assert.Equal("Våning 2", second!.Floor);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var result = await CreateUseCase().HandleAsync(Guid.NewGuid(), "Kök", CancellationToken.None);

        Assert.Null(result);
    }
}
