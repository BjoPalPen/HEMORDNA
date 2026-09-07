using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class MoveTaskToAreaTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private MoveTaskToArea CreateUseCase() => new(_households, _definitions);

    private async Task<Household> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        household.AddArea("Kök");
        await _households.UpdateAsync(household, CancellationToken.None);

        return household;
    }

    [Fact]
    public async Task Moves_the_task_to_a_different_room()
    {
        var household = await ArrangeHouseholdAsync();
        var kok = household.Areas.Single(a => a.Name == "Kök");

        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(household.Id, task.Id, kok.Id, CancellationToken.None);

        Assert.Equal(kok.Id, result!.AreaId);
    }

    [Fact]
    public async Task Clearing_the_area_moves_the_task_to_Ovrigt()
    {
        var household = await ArrangeHouseholdAsync();
        var kok = household.Areas.Single(a => a.Name == "Kök");

        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        task.AssignToArea(kok.Id);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(household.Id, task.Id, null, CancellationToken.None);

        Assert.Null(result!.AreaId);
    }

    [Fact]
    public async Task Rejects_an_area_from_another_household()
    {
        var household = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        _definitions.Seed(task);

        var strangerAreaId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateUseCase().HandleAsync(household.Id, task.Id, strangerAreaId, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        Assert.Null(result);
    }
}
