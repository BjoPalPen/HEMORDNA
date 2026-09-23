using Hemordna.Application.Push;
using Hemordna.Domain.Push;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class PushSubscriptionRepository : IPushSubscriptionRepository
{
    private readonly HemordnaDbContext _dbContext;

    public PushSubscriptionRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        await _dbContext.PushSubscriptions.AddAsync(subscription, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken)
        => _dbContext.PushSubscriptions
            .FirstOrDefaultAsync(subscription => subscription.Endpoint == endpoint, cancellationToken);

    public Task UpdateAsync(PushSubscription subscription, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(PushSubscription subscription, CancellationToken cancellationToken)
    {
        _dbContext.PushSubscriptions.Remove(subscription);
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PushSubscription>> ListForMemberAsync(
        Guid householdId, Guid memberId, CancellationToken cancellationToken)
        => await _dbContext.PushSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.HouseholdId == householdId && subscription.MemberId == memberId)
            .ToListAsync(cancellationToken);
}
