using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class ChangeTaskEffortTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private ChangeTaskEffort CreateUseCase() => new(_definitions);

    [Fact]
    public async Task Changes_the_effort_level()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Skrubba dusch", 15, Now);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, TaskEffort.Heavy, CancellationToken.None);

        Assert.Equal(TaskEffort.Heavy, result!.Effort);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), TaskEffort.Light, CancellationToken.None);

        Assert.Null(result);
    }
}
