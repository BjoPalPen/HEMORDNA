using Hemordna.Application.Households;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class PauseAreaTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 2, 10);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();

    private PauseArea CreateUseCase() => new(_households, _definitions, _occurrences);

    private async Task<(Guid HouseholdId, Area Area, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var area = household.AddArea("Badrum");
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, area, household.Members.Single());
    }

    private TaskDefinition SeedTask(Guid householdId, Guid areaId, string name)
    {
        var definition = TaskDefinition.Create(householdId, name, 10, Now);
        definition.AssignToArea(areaId);
        _definitions.Seed(definition);
        return definition;
    }

    [Fact]
    public async Task Pauses_the_area()
    {
        var (householdId, area, _) = await ArrangeHouseholdAsync();
        var until = Today.AddDays(7);

        var paused = await CreateUseCase().HandleAsync(householdId, area.Id, until, CancellationToken.None);

        Assert.NotNull(paused);
        Assert.Equal(until, paused.PausedUntil);
        Assert.Equal(until, area.PausedUntil);
    }

    [Fact]
    public async Task Skips_an_outstanding_occurrence_scheduled_within_the_pause_window()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var task = SeedTask(householdId, area.Id, "Skrubba dusch");
        var occurrence = task.ScheduleFor(Today, Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, Today.AddDays(7), CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Skipped, occurrence.Status);
    }

    [Fact]
    public async Task Leaves_an_occurrence_scheduled_after_the_pause_window_alone()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var task = SeedTask(householdId, area.Id, "Skrubba dusch");
        var occurrence = task.ScheduleFor(Today.AddDays(30), Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, Today.AddDays(7), CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    [Fact]
    public async Task Leaves_another_rooms_occurrence_alone()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var household = (await _households.FindByIdAsync(householdId, CancellationToken.None))!;
        var otherArea = household.AddArea("Kök");
        await _households.UpdateAsync(household, CancellationToken.None);

        var otherTask = SeedTask(householdId, otherArea.Id, "Diska");
        var occurrence = otherTask.ScheduleFor(Today, Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, Today.AddDays(7), CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    [Fact]
    public async Task Resuming_does_not_touch_any_occurrence()
    {
        var (householdId, area, member) = await ArrangeHouseholdAsync();
        var task = SeedTask(householdId, area.Id, "Skrubba dusch");
        var occurrence = task.ScheduleFor(Today.AddDays(30), Now);
        occurrence.AssignTo(member.Id);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(householdId, area.Id, Today.AddDays(7), CancellationToken.None);
        var resumed = await CreateUseCase().HandleAsync(householdId, area.Id, null, CancellationToken.None);

        Assert.NotNull(resumed);
        Assert.Null(resumed.PausedUntil);
        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    [Fact]
    public async Task Returns_null_for_an_area_outside_the_household()
    {
        var (householdId, _, _) = await ArrangeHouseholdAsync();

        var paused = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), Today.AddDays(7), CancellationToken.None);

        Assert.Null(paused);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var paused = await CreateUseCase().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Today.AddDays(7), CancellationToken.None);

        Assert.Null(paused);
    }
}
