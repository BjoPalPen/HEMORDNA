using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

internal sealed class InMemoryPlanCandidateQuery : IPlanCandidateQuery
{
    private readonly List<(Guid MemberId, PlanCandidate Candidate)> _candidates = [];

    internal void AssignToMember(Guid memberId, PlanCandidate candidate)
        => _candidates.Add((memberId, candidate));

    // Both filters mirror the real query so the use case tests exercise the same shape.
    public Task<IReadOnlyList<PlanCandidate>> FindOutstandingForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken)
        => Query(householdId, memberId, c =>
            c.Occurrence.Status == TaskOccurrenceStatus.Planned
            && c.Occurrence.ScheduledDate <= onOrBefore);

    public Task<IReadOnlyList<PlanCandidate>> FindCompletedForMemberOnAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken)
        => Query(householdId, memberId, c =>
            c.Occurrence.Status == TaskOccurrenceStatus.Completed
            && c.Occurrence.ScheduledDate == date);

    private Task<IReadOnlyList<PlanCandidate>> Query(
        Guid householdId,
        Guid memberId,
        Func<PlanCandidate, bool> filter)
    {
        IReadOnlyList<PlanCandidate> result =
        [
            .. _candidates
                .Where(entry => entry.MemberId == memberId
                    && entry.Candidate.Occurrence.HouseholdId == householdId
                    && filter(entry.Candidate))
                .Select(entry => entry.Candidate)
        ];

        return Task.FromResult(result);
    }
}
