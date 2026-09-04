using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class MemberAvailabilityRepository : IMemberAvailabilityRepository
{
    private readonly HemordnaDbContext _dbContext;

    public MemberAvailabilityRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public Task<MemberAvailability?> FindAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken)
        => _dbContext.MemberAvailabilities
            .FirstOrDefaultAsync(
                availability => availability.HouseholdId == householdId
                    && availability.MemberId == memberId
                    && availability.Date == date,
                cancellationToken);

    public async Task AddAsync(MemberAvailability availability, CancellationToken cancellationToken)
    {
        await _dbContext.MemberAvailabilities.AddAsync(availability, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(MemberAvailability availability, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
