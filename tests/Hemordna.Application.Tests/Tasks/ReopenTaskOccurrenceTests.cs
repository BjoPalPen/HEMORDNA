using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class ReopenTaskOccurrenceTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 2, 6, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();

    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private TaskOccurrence Seed(DateTimeOffset now)
    {
        var definition = TaskDefinition.Create(HouseholdId, "Hall", 7, now);
        var occurrence = definition.ScheduleFor(Friday, now);
        occurrence.Complete(AnnaId, CompletedAt);
        _occurrences.Seed(occurrence);
        return occurrence;
    }

    private ReopenTaskOccurrence Reopen(DateTimeOffset now) => new(_occurrences, _notifier, new FixedTimeProvider(now));

    [Fact]
    public async Task Reopening_within_the_window_notifies_exactly_once()
    {
        var occurrence = Seed(CompletedAt);
        var now = CompletedAt.AddMinutes(5);

        var result = await Reopen(now).HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
        Assert.Null(occurrence.CompletedByMemberId);
        Assert.Equal(1, _occurrences.UpdateCallCount);
        Assert.Equal(1, _notifier.CallCount);
        Assert.True(_notifier.WasNotified(HouseholdId));
    }

    [Fact]
    public async Task An_unknown_occurrence_finds_nothing()
    {
        var result = await Reopen(CompletedAt)
            .HandleAsync(HouseholdId, Guid.NewGuid(), AnnaId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Reopening_as_someone_else_is_rejected_and_persists_nothing()
    {
        var occurrence = Seed(CompletedAt);
        var bjornId = Guid.NewGuid();

        await Assert.ThrowsAsync<DomainException>(() =>
            Reopen(CompletedAt.AddMinutes(1)).HandleAsync(HouseholdId, occurrence.Id, bjornId, CancellationToken.None));

        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
        Assert.Equal(0, _occurrences.UpdateCallCount);
        Assert.Equal(0, _notifier.CallCount);
    }

    [Fact]
    public async Task Reopening_outside_the_window_is_rejected()
    {
        var occurrence = Seed(CompletedAt);

        await Assert.ThrowsAsync<DomainException>(() =>
            Reopen(CompletedAt.AddMinutes(20)).HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None));

        Assert.Equal(0, _occurrences.UpdateCallCount);
    }
}
