using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

internal sealed class InMemoryMemberAvailabilityRepository : IMemberAvailabilityRepository
{
    private readonly List<MemberAvailability> _availabilities = [];

    internal int Count => _availabilities.Count;

    public Task<MemberAvailability?> FindAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken)
        => Task.FromResult(_availabilities.FirstOrDefault(a =>
            a.HouseholdId == householdId && a.MemberId == memberId && a.Date == date));

    public Task AddAsync(MemberAvailability availability, CancellationToken cancellationToken)
    {
        _availabilities.Add(availability);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemberAvailability availability, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
