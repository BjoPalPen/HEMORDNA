using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class ChangeTaskEstimatedMinutesTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private ChangeTaskEstimatedMinutes CreateUseCase() => new(_definitions);

    [Fact]
    public async Task Changes_the_estimate()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Dammsug vardagsrum", 15, Now);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, estimatedMinutes: 30, CancellationToken.None);

        Assert.Equal(30, result!.EstimatedMinutes);
    }

    [Fact]
    public async Task Accepts_zero_as_a_deliberate_choice()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Dammsug vardagsrum", 15, Now);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, estimatedMinutes: 0, CancellationToken.None);

        Assert.Equal(0, result!.EstimatedMinutes);
    }

    [Fact]
    public async Task Rejects_a_negative_estimate()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Dammsug vardagsrum", 15, Now);
        _definitions.Seed(task);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, estimatedMinutes: -15, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), estimatedMinutes: 30, CancellationToken.None);

        Assert.Null(result);
    }
}
