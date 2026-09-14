using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class DeactivateTaskDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();

    private DeactivateTaskDefinition CreateUseCase() => new(_definitions, _occurrences);

    private TaskDefinition SeedTask(string name)
    {
        var definition = TaskDefinition.Create(HouseholdId, name, 10, Now);
        _definitions.Seed(definition);
        return definition;
    }

    private TaskOccurrence SeedOccurrence(TaskDefinition definition, DateOnly date)
    {
        var occurrence = definition.ScheduleFor(date, Now);
        occurrence.AssignTo(MemberId);
        _occurrences.Seed(occurrence);
        return occurrence;
    }

    [Fact]
    public async Task Deactivates_the_task()
    {
        var definition = SeedTask("Vädra rummet");

        var deactivated = await CreateUseCase().HandleAsync(HouseholdId, definition.Id, CancellationToken.None);

        Assert.NotNull(deactivated);
        Assert.False(definition.IsActive);
    }

    /// <summary>Utan detta ligger redan utlagda förekomster kvar på någons dag och namnger en
    /// uppgift hushållet sagt att de inte gör längre - se DeactivateTaskDefinition.</summary>
    [Fact]
    public async Task Skips_occurrences_already_scheduled_from_it()
    {
        var definition = SeedTask("Bädda sängen");
        var occurrence = SeedOccurrence(definition, new DateOnly(2026, 2, 10));

        await CreateUseCase().HandleAsync(HouseholdId, definition.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Skipped, occurrence.Status);
    }

    [Fact]
    public async Task Leaves_another_tasks_occurrences_alone()
    {
        var definition = SeedTask("Bädda sängen");
        var other = SeedTask("Diska");
        var occurrence = SeedOccurrence(other, new DateOnly(2026, 2, 10));

        await CreateUseCase().HandleAsync(HouseholdId, definition.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    [Fact]
    public async Task Leaves_a_completed_occurrence_alone()
    {
        var definition = SeedTask("Bädda sängen");
        var occurrence = definition.ScheduleFor(new DateOnly(2026, 2, 4), Now);
        occurrence.AssignTo(MemberId);
        occurrence.Complete(MemberId, Now);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(HouseholdId, definition.Id, CancellationToken.None);

        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var deactivated = await CreateUseCase().HandleAsync(HouseholdId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(deactivated);
    }
}
