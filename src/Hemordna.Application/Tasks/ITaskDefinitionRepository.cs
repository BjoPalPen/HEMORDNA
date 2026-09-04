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
}
