using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class BringOccurrenceForwardTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly DateOnly Saturday = Friday.AddDays(1);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();

    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private BringOccurrenceForward CreateUseCase() => new(_occurrences, _notifier);

    private TaskOccurrence Seed(DateOnly date, Guid? assignedMemberId)
    {
        var definition = TaskDefinition.Create(HouseholdId, "Diska", 10, Now);
        var occurrence = definition.ScheduleFor(date, Now);

        if (assignedMemberId is { } memberId)
        {
            occurrence.AssignTo(memberId);
        }

        _occurrences.Seed(occurrence);
        return occurrence;
    }

    [Fact]
    public async Task Brings_the_callers_own_occurrence_forward_and_notifies_once()
    {
        var occurrence = Seed(Saturday, AnnaId);

        var result = await CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, Friday, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(Friday, occurrence.ScheduledDate);
        Assert.Equal(Saturday, occurrence.OriginalScheduledDate);
        Assert.Equal(1, _occurrences.UpdateCallCount);
        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task An_unknown_occurrence_finds_nothing()
    {
        var result = await CreateUseCase().HandleAsync(HouseholdId, Guid.NewGuid(), AnnaId, Friday, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Bringing_forward_someone_elses_occurrence_is_rejected()
    {
        var occurrence = Seed(Saturday, AnnaId);
        var bjornId = Guid.NewGuid();

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, bjornId, Friday, CancellationToken.None));

        Assert.Equal(Saturday, occurrence.ScheduledDate);
        Assert.Equal(0, _occurrences.UpdateCallCount);
        Assert.Equal(0, _notifier.CallCount);
    }

    [Fact]
    public async Task An_unassigned_occurrence_can_never_be_brought_forward_by_anyone()
    {
        var occurrence = Seed(Saturday, assignedMemberId: null);

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, Friday, CancellationToken.None));
    }

    [Fact]
    public async Task An_occurrence_already_due_today_cannot_be_brought_forward()
    {
        var occurrence = Seed(Friday, AnnaId);

        await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().HandleAsync(HouseholdId, occurrence.Id, AnnaId, Friday, CancellationToken.None));

        Assert.Equal(0, _occurrences.UpdateCallCount);
    }
}
