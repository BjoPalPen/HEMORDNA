using Hemordna.Application.Households;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class DeactivateAreaTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private DeactivateArea CreateUseCase() => new(_households, _definitions);

    private async Task<(Guid HouseholdId, Area Area, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = household.AddArea("Litet wc");
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, area, household.Members.Single());
    }

    [Fact]
    public async Task Deactivates_the_area()
    {
        var (householdId, area, _) = await ArrangeHouseholdAsync();

        var deactivated = await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);
        Assert.False(area.IsActive);
    }

    [Fact]
    public async Task Also_deactivates_the_areas_own_active_tasks()
    {
        var (householdId, area, _) = await ArrangeHouseholdAsync();

        var task = TaskDefinition.Create(householdId, "Rengör toalettstolen", 10, Now);
        task.AssignToArea(area.Id);
        _definitions.Seed(task);

        var otherRoomTask = TaskDefinition.Create(householdId, "Diska", 15, Now);
        _definitions.Seed(otherRoomTask);

        await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.False(task.IsActive);
        Assert.True(otherRoomTask.IsActive);
    }

    [Fact]
    public async Task Leaves_an_already_inactive_task_alone()
    {
        var (householdId, area, _) = await ArrangeHouseholdAsync();

        var task = TaskDefinition.Create(householdId, "Rengör toalettstolen", 10, Now);
        task.AssignToArea(area.Id);
        task.Deactivate();
        _definitions.Seed(task);

        await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.False(task.IsActive);
    }

    [Fact]
    public async Task Returns_null_for_an_area_outside_the_household()
    {
        var (householdId, _, _) = await ArrangeHouseholdAsync();

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
