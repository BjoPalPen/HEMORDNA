namespace Hemordna.Application.Planning;

/// <summary>
/// Reads the task instances that make up a member's day. A query, not a repository:
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

    /// <summary>
    /// What this member already finished on <paramref name="date"/>. The day is not only what
    /// is left - showing what is done is what lets the screen say "det viktigaste är gjort"
    /// instead of only counting what remains.
    /// </summary>
    Task<IReadOnlyList<PlanCandidate>> FindCompletedForMemberOnAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken);
}
