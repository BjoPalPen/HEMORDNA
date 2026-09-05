using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class HouseholdRepository : IHouseholdRepository
{
    private readonly HemordnaDbContext _dbContext;

    public HouseholdRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Household household, CancellationToken cancellationToken)
    {
        await _dbContext.Households.AddAsync(household, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Household household, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<Household?> FindByIdAsync(Guid householdId, CancellationToken cancellationToken)
        => _dbContext.Households
            .Include(household => household.Members)
            .Include(household => household.Areas)
            .FirstOrDefaultAsync(household => household.Id == householdId, cancellationToken);

    public Task<Household?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
        => _dbContext.Households
            .Include(household => household.Members)
            .Include(household => household.Areas)
            .FirstOrDefaultAsync(household => household.InviteCode == inviteCode, cancellationToken);
}
