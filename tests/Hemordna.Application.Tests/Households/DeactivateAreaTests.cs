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
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();

    private DeactivateArea CreateUseCase() => new(_households, _definitions, _occurrences);

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

    /// <summary>Ett riktigt hushåll bytte två sovrum mot våningsnamngivna, och de borttagna
    /// rummens redan utlagda förekomster levde vidare - 32 planerade uppgifter som fortsatte
    /// visa "Sovrum 1"/"Sovrum 2" bland dagens arbete i veckor. Se DeactivateArea.</summary>
    [Fact]
    public async Task Skips_occurrences_already_scheduled_from_the_removed_rooms_tasks()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Bädda sängen", 10, Now);
        definition.AssignToArea(area.Id);
        _definitions.Seed(definition);

        var occurrence = definition.ScheduleFor(new DateOnly(2026, 2, 10), Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Skipped, occurrence.Status);
    }

    [Fact]
    public async Task Leaves_another_rooms_occurrences_alone()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var household = (await _households.FindByIdAsync(householdId, CancellationToken.None))!;
        var otherArea = household.AddArea("Kök");
        await _households.UpdateAsync(household, CancellationToken.None);

        var otherDefinition = TaskDefinition.Create(householdId, "Diska", 10, Now);
        otherDefinition.AssignToArea(otherArea.Id);
        _definitions.Seed(otherDefinition);

        var occurrence = otherDefinition.ScheduleFor(new DateOnly(2026, 2, 10), Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    /// <summary>Historiken om vad som FAKTISKT gjordes ska stå kvar - en borttagning säger
    /// bara att rummet inte finns längre, inte att arbetet aldrig utfördes.</summary>
    [Fact]
    public async Task Leaves_a_completed_occurrence_alone()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Vädra rummet", 5, Now);
        definition.AssignToArea(area.Id);
        _definitions.Seed(definition);

        var occurrence = definition.ScheduleFor(new DateOnly(2026, 2, 4), Now);
        occurrence.AssignTo(member.Id);
        occurrence.Complete(member.Id, Now);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
    }
}
