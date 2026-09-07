using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class UpdateTaskFrequencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 2);

    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();

    private UpdateTaskFrequency CreateUseCase() => new(_definitions, _occurrences);

    [Fact]
    public async Task Changes_a_dailys_recurrence_to_weekly()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Diska", 10, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Today));
        _definitions.Seed(definition);

        var weekly = RecurrenceRule.Weekly(Today, Today.DayOfWeek);
        var result = await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, weekly, null, CancellationToken.None);

        Assert.Equal(weekly, result!.Recurrence);
    }

    [Fact]
    public async Task Switching_to_as_needed_clears_the_recurrence()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Putsa fönster", 10, Now);
        definition.SetRecurrence(RecurrenceRule.Monthly(Today));
        _definitions.Seed(definition);

        var result = await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, null, 21, CancellationToken.None);

        Assert.Null(result!.Recurrence);
        Assert.Equal(21, result.StaleAfterDays);
    }

    [Fact]
    public async Task Switching_to_a_recurrence_clears_any_as_needed_interval()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Damma hyllor", 10, Now);
        definition.SetStaleAfterDays(21);
        _definitions.Seed(definition);

        var weekly = RecurrenceRule.Weekly(Today, Today.DayOfWeek);
        var result = await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, weekly, null, CancellationToken.None);

        Assert.Equal(weekly, result!.Recurrence);
        Assert.Null(result.StaleAfterDays);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), RecurrenceRule.Daily(Today), null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Actually_changing_the_recurrence_skips_the_still_outstanding_occurrence()
    {
        // The production bug this fixes: a room's "Torka golvet" moved to a new weekday leaves
        // its old, already-generated occurrence behind - forever outstanding, endlessly
        // deferred, alongside a fresh one EnsureOccurrencesGenerated creates once the new
        // weekday arrives. Left unresolved, the same chore nags twice, permanently.
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Torka golvet", 10, Now);
        definition.SetRecurrence(RecurrenceRule.Weekly(Today, DayOfWeek.Monday));
        _definitions.Seed(definition);
        var outstanding = definition.ScheduleFor(Today, Now);
        _occurrences.Seed(outstanding);

        await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, RecurrenceRule.Weekly(Today, DayOfWeek.Wednesday), null,
            CancellationToken.None);

        Assert.False(await _occurrences.HasOutstandingAsync(definition.HouseholdId, definition.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Resaving_the_exact_same_recurrence_does_not_touch_the_outstanding_occurrence()
    {
        // Opening "Ändra frekvens" and hitting Spara without actually changing anything must
        // not silently drop a task someone is already about to do today.
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Torka golvet", 10, Now);
        var weekly = RecurrenceRule.Weekly(Today, DayOfWeek.Monday);
        definition.SetRecurrence(weekly);
        _definitions.Seed(definition);
        var outstanding = definition.ScheduleFor(Today, Now);
        _occurrences.Seed(outstanding);

        await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, RecurrenceRule.Weekly(Today, DayOfWeek.Monday), null,
            CancellationToken.None);

        Assert.True(await _occurrences.HasOutstandingAsync(definition.HouseholdId, definition.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Switching_to_as_needed_also_skips_the_still_outstanding_occurrence()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Putsa fönster", 10, Now);
        definition.SetRecurrence(RecurrenceRule.Monthly(Today));
        _definitions.Seed(definition);
        var outstanding = definition.ScheduleFor(Today, Now);
        _occurrences.Seed(outstanding);

        await CreateUseCase().HandleAsync(
            definition.HouseholdId, definition.Id, null, 21, CancellationToken.None);

        Assert.False(await _occurrences.HasOutstandingAsync(definition.HouseholdId, definition.Id, CancellationToken.None));
    }
}
