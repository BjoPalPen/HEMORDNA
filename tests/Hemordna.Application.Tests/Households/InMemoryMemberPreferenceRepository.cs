using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

internal sealed class InMemoryMemberPreferenceRepository : IMemberPreferenceRepository
{
    private readonly List<MemberPreference> _preferences = [];

    internal int Count => _preferences.Count;

    public Task<MemberPreference?> FindAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken)
        => Task.FromResult(_preferences.FirstOrDefault(
            p => p.HouseholdId == householdId && p.MemberId == memberId));

    public Task AddAsync(MemberPreference preference, CancellationToken cancellationToken)
    {
        _preferences.Add(preference);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(MemberPreference preference, CancellationToken cancellationToken) => Task.CompletedTask;
}
