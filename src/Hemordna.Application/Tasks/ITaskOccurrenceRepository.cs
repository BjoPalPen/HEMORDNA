using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Concrete scheduled instances of tasks.</summary>
public interface ITaskOccurrenceRepository
{
    Task AddAsync(TaskOccurrence occurrence, CancellationToken cancellationToken);

    Task<TaskOccurrence?> FindByIdAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken);

    Task UpdateAsync(TaskOccurrence occurrence, CancellationToken cancellationToken);
}
