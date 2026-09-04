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

    /// <summary>
    /// The <see cref="TaskOccurrence.OriginalScheduledDate"/> of the most recently scheduled
    /// occurrence for this definition, or null if none exist yet. Used to find where automatic
    /// recurrence generation left off, regardless of status or later deferrals.
    /// </summary>
    Task<DateOnly?> FindMostRecentOriginalDateAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);
}
