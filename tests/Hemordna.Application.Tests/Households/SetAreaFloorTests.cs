using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class SetAreaFloorTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private SetAreaFloor CreateUseCase() => new(_households);

    [Fact]
    public async Task Moves_the_area_to_a_new_floor()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = household.AddArea("Kök", floor: "Våning 1");
        await _households.UpdateAsync(household, CancellationToken.None);

        var result = await CreateUseCase().HandleAsync(household.Id, area.Id, "Våning 2", CancellationToken.None);

        Assert.Equal("Våning 2", result!.Floor);
    }

    [Fact]
    public async Task Rejects_a_move_to_a_floor_where_the_name_is_already_taken()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        household.AddArea("Hall", floor: "Våning 2");
        var area = household.AddArea("Hall", floor: "Våning 1");
        await _households.UpdateAsync(household, CancellationToken.None);

        await Assert.ThrowsAsync<Domain.Common.DomainException>(
            () => CreateUseCase().HandleAsync(household.Id, area.Id, "Våning 2", CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_area_outside_the_household()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var result = await CreateUseCase().HandleAsync(household.Id, Guid.NewGuid(), "Våning 2", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var result = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), "Våning 2", CancellationToken.None);

        Assert.Null(result);
    }
}
