using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class UpdateTaskFrequencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 2);

    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private UpdateTaskFrequency CreateUseCase() => new(_definitions);

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
}
