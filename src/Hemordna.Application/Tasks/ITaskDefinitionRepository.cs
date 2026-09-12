using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Task definitions for a household. Every read takes the household id: the tenant boundary
/// is part of the operation, not something a caller can forget to apply.
/// </summary>
public interface ITaskDefinitionRepository
{
    Task AddAsync(TaskDefinition definition, CancellationToken cancellationToken);

    Task<TaskDefinition?> FindByIdAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskDefinition>> ListByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);

    /// <summary>Active task definitions in a given area, tracked so callers can mutate and save them.</summary>
    Task<IReadOnlyList<TaskDefinition>> ListActiveByAreaAsync(
        Guid householdId,
        Guid areaId,
        CancellationToken cancellationToken);

    /// <summary>Persists changes made to a task definition loaded through this repository.</summary>
    Task UpdateAsync(TaskDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// Hard-deletes every task definition in the household - and with it, by database cascade,
    /// every occurrence and assignment row that referenced one. Used only by
    /// <c>ResetHousehold</c>; every other removal in the app is a deactivation, not a delete.
    /// </summary>
    Task DeleteAllByHouseholdAsync(Guid householdId, CancellationToken cancellationToken);
}
