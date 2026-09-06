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

    /// <summary>
    /// Total assigned minutes per member, summed across every assignment this household has
    /// ever recorded - what <see cref="RotationPicker"/> weighs against each member's
    /// available time to decide who is next. A member with no assignments yet is simply absent
    /// from the result, not present with zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAssignedMinutesByMemberAsync(
        Guid householdId,
        CancellationToken cancellationToken);
}
