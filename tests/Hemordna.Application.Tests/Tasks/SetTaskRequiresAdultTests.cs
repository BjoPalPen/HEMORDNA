using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class SetTaskRequiresAdultTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private SetTaskRequiresAdult CreateUseCase() => new(_definitions);

    [Fact]
    public async Task Turns_the_flag_on()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Tvätta fönster", 15, Now);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, requiresAdult: true, CancellationToken.None);

        Assert.True(result!.RequiresAdult);
    }

    [Fact]
    public async Task Turns_the_flag_off()
    {
        var task = TaskDefinition.Create(Guid.NewGuid(), "Tvätta fönster", 15, Now);
        task.SetRequiresAdult(true);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            task.HouseholdId, task.Id, requiresAdult: false, CancellationToken.None);

        Assert.False(result!.RequiresAdult);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), requiresAdult: true, CancellationToken.None);

        Assert.Null(result);
    }
}
