using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

internal sealed class InMemoryTaskDefinitionRepository : ITaskDefinitionRepository
{
    private readonly List<TaskDefinition> _definitions = [];

    internal void Seed(TaskDefinition definition) => _definitions.Add(definition);

    public Task AddAsync(TaskDefinition definition, CancellationToken cancellationToken)
    {
        _definitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task<TaskDefinition?> FindByIdAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_definitions.FirstOrDefault(
            d => d.HouseholdId == householdId && d.Id == taskDefinitionId));

    public Task<IReadOnlyList<TaskDefinition>> ListByHouseholdAsync(
        Guid householdId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TaskDefinition>>(
            [.. _definitions.Where(d => d.HouseholdId == householdId)]);
}
