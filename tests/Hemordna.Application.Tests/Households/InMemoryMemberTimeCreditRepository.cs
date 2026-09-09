using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

internal sealed class InMemoryMemberTimeCreditRepository : IMemberTimeCreditRepository
{
    private readonly List<MemberTimeCredit> _entries = [];

    internal int Count => _entries.Count;

    internal void Seed(MemberTimeCredit entry) => _entries.Add(entry);

    public Task<IReadOnlyList<MemberTimeCredit>> ListForMemberAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<MemberTimeCredit>>(_entries
            .Where(e => e.HouseholdId == householdId && e.MemberId == memberId && e.OccurredOn >= from && e.OccurredOn <= to)
            .ToList());

    public Task AddAsync(MemberTimeCredit entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task RemoveByOccurrenceAsync(
        Guid occurrenceId, IReadOnlyCollection<TimeCreditReason> reasons, CancellationToken cancellationToken)
    {
        _entries.RemoveAll(e => e.OccurrenceId == occurrenceId && reasons.Contains(e.Reason));
        return Task.CompletedTask;
    }
}
