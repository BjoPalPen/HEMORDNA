using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class CompleteAndDeferTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();

    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private TaskOccurrence Seed(bool canBeDeferred = true)
    {
        var definition = TaskDefinition.Create(HouseholdId, "Hall", 7, Now);
        definition.SetCanBeDeferred(canBeDeferred);
        var occurrence = definition.ScheduleFor(Friday, Now);
        _occurrences.Seed(occurrence);
        return occurrence;
    }

    private CompleteTaskOccurrence Complete() => new(_occurrences, _notifier, new FixedTimeProvider(Now));

    private DeferTaskOccurrence Defer() => new(_occurrences, _notifier);

    [Fact]
    public async Task Completing_records_the_caller_and_the_injected_time()
    {
        var occurrence = Seed();

        var result = await Complete().HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(TaskOccurrenceStatus.Completed, result.Status);
        Assert.Equal(AnnaId, result.CompletedByMemberId);
        Assert.Equal(Now, result.CompletedAt);
        Assert.Equal(1, _occurrences.UpdateCallCount);
        Assert.True(_notifier.WasNotified(HouseholdId));
    }

    [Fact]
    public async Task Completing_twice_keeps_the_first_completion()
    {
        // Two people tapping the same task must not rewrite who finished it.
        var occurrence = Seed();
        var bjornId = Guid.NewGuid();

        await Complete().HandleAsync(HouseholdId, occurrence.Id, AnnaId, CancellationToken.None);
        var second = await Complete().HandleAsync(HouseholdId, occurrence.Id, bjornId, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(AnnaId, second.CompletedByMemberId);
    }

    [Fact]
    public async Task Completing_an_occurrence_in_another_household_finds_nothing()
    {
        var occurrence = Seed();

        var result = await Complete()
            .HandleAsync(Guid.NewGuid(), occurrence.Id, AnnaId, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _occurrences.UpdateCallCount);
    }

    [Fact]
    public async Task Deferring_moves_the_date_and_keeps_it_outstanding()
    {
        var occurrence = Seed();
        var monday = Friday.AddDays(3);

        var result = await Defer().HandleAsync(HouseholdId, occurrence.Id, monday, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(monday, result.ScheduledDate);
        Assert.Equal(Friday, result.OriginalScheduledDate);
        Assert.True(result.IsOutstanding);
    }

    [Fact]
    public async Task A_task_that_cannot_be_deferred_is_rejected()
    {
        var occurrence = Seed(canBeDeferred: false);

        await Assert.ThrowsAsync<DomainException>(
            () => Defer().HandleAsync(HouseholdId, occurrence.Id, Friday.AddDays(1), CancellationToken.None));

        Assert.Equal(0, _occurrences.UpdateCallCount);
    }

    [Fact]
    public async Task A_task_cannot_be_deferred_backwards()
    {
        var occurrence = Seed();

        await Assert.ThrowsAsync<DomainException>(
            () => Defer().HandleAsync(HouseholdId, occurrence.Id, Friday.AddDays(-1), CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_occurrence_finds_nothing()
    {
        var result = await Defer()
            .HandleAsync(HouseholdId, Guid.NewGuid(), Friday.AddDays(1), CancellationToken.None);

        Assert.Null(result);
    }
}
