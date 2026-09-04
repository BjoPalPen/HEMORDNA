using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>History of who was assigned a task definition, used to rotate responsibility.</summary>
public interface ITaskAssignmentRepository
{
    Task AddAsync(TaskAssignment assignment, CancellationToken cancellationToken);

    /// <summary>The most recent assignment for this definition, by scheduled date, or null if none exist.</summary>
    Task<TaskAssignment?> FindMostRecentAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);
}
