using Hemordna.Application.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class HouseholdMembershipQuery : IHouseholdMembershipQuery
{
    private readonly HemordnaDbContext _dbContext;

    public HouseholdMembershipQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<HouseholdMembership?> FindByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId && member.IsActive)
            .Select(member => new HouseholdMembership(member.HouseholdId, member.Id))
            .FirstOrDefaultAsync(cancellationToken);
}
