namespace Hemordna.Application.Planning;

/// <summary>
/// Reads the task instances that could go on a member's day. A query, not a repository:
/// it only reads, and it returns planning models rather than aggregates.
/// </summary>
public interface IPlanCandidateQuery
{
    /// <summary>
    /// Outstanding occurrences assigned to this member, scheduled on or before
    /// <paramref name="onOrBefore"/>, paired with their task names.
    /// </summary>
    Task<IReadOnlyList<PlanCandidate>> FindOutstandingForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken);
}
