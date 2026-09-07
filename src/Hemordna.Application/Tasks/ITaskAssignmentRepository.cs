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

    /// <summary>
    /// Assigned minutes per member on one specific <paramref name="date"/> only - what
    /// <see cref="RotationPicker"/> weighs against each member's OWN available minutes that
    /// day, so a large all-time imbalance (see <see cref="GetAssignedMinutesByMemberAsync"/>)
    /// cannot be corrected by dumping an entire backlog onto one person in a single sitting.
    /// A member with no assignment on this date is absent from the result, not present with
    /// zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAssignedMinutesByMemberOnDateAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken);
}
