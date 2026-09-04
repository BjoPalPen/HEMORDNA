using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

internal sealed class InMemoryPlanCandidateQuery : IPlanCandidateQuery
{
    private readonly List<(Guid MemberId, PlanCandidate Candidate)> _candidates = [];

    internal void AssignToMember(Guid memberId, PlanCandidate candidate)
        => _candidates.Add((memberId, candidate));

    public Task<IReadOnlyList<PlanCandidate>> FindOutstandingForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken)
    {
        // Mirrors the real query's filter so the use case tests exercise the same shape.
        IReadOnlyList<PlanCandidate> result =
        [
            .. _candidates
                .Where(entry => entry.MemberId == memberId
                    && entry.Candidate.Occurrence.HouseholdId == householdId
                    && entry.Candidate.Occurrence.Status == TaskOccurrenceStatus.Planned
                    && entry.Candidate.Occurrence.ScheduledDate <= onOrBefore)
                .Select(entry => entry.Candidate)
        ];

        return Task.FromResult(result);
    }
}
