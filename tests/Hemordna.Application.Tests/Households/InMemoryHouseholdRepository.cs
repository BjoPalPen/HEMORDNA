using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

/// <summary>
/// A hand-written fake. The use cases are thin enough that a real database would test EF
/// Core rather than the use case.
/// </summary>
internal sealed class InMemoryHouseholdRepository : IHouseholdRepository
{
    private readonly Dictionary<Guid, Household> _households = [];

    internal int AddCallCount { get; private set; }

    internal int UpdateCallCount { get; private set; }

    public Task AddAsync(Household household, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _households[household.Id] = household;
        return Task.CompletedTask;
    }

    public Task<Household?> FindByIdAsync(Guid householdId, CancellationToken cancellationToken)
        => Task.FromResult(_households.GetValueOrDefault(householdId));

    public Task<Household?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
        => Task.FromResult(_households.Values.FirstOrDefault(household => household.InviteCode == inviteCode));

    public Task UpdateAsync(Household household, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        _households[household.Id] = household;
        return Task.CompletedTask;
    }
}
