using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class UndoBringForwardTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly DateOnly Saturday = Friday.AddDays(1);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();

    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private UndoBringForward CreateUseCase() => new(_occurrences, _notifier);

    private TaskOccurrence SeedBroughtForward()
    {
        var definition = TaskDefinition.Create(HouseholdId, "Diska", 10, Now);
        var occurrence = definition.ScheduleFor(Saturday, Now);
        occurrence.AssignTo(AnnaId);
        occurrence.BringForwardTo(Friday);
        _occurrences.Seed(occurrence);
        return occurrence;
    }

    [Fact]
    public async Task Puts_the_occurrence_back_on_its_original_date_and_notifies_once()
    {
        var occurrence = SeedBroughtForward();

        var result = await CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(Saturday, occurrence.ScheduledDate);
        Assert.Equal(1, _occurrences.UpdateCallCount);
        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task An_unknown_occurrence_finds_nothing()
    {
        var result = await CreateUseCase().HandleAsync(HouseholdId, Guid.NewGuid(), AnnaId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Undoing_someone_elses_bring_forward_is_rejected()
    {
        var occurrence = SeedBroughtForward();
        var bjornId = Guid.NewGuid();

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, bjornId, CancellationToken.None));

        Assert.Equal(Friday, occurrence.ScheduledDate);
        Assert.Equal(0, _occurrences.UpdateCallCount);
    }

    [Fact]
    public async Task An_occurrence_that_was_never_brought_forward_cannot_be_undone()
    {
        var definition = TaskDefinition.Create(HouseholdId, "Diska", 10, Now);
        var occurrence = definition.ScheduleFor(Friday, Now);
        occurrence.AssignTo(AnnaId);
        _occurrences.Seed(occurrence);

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None));

        Assert.Equal(0, _occurrences.UpdateCallCount);
    }

    [Fact]
    public async Task Works_even_for_a_non_deferrable_task()
    {
        var definition = TaskDefinition.Create(HouseholdId, "Fast uppgift", 10, Now);
        definition.SetCanBeDeferred(false);
        var occurrence = definition.ScheduleFor(Saturday, Now);
        occurrence.AssignTo(AnnaId);
        occurrence.BringForwardTo(Friday);
        _occurrences.Seed(occurrence);

        var result = await CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(Saturday, occurrence.ScheduledDate);
    }
}
