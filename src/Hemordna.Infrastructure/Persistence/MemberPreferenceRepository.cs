using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class MemberPreferenceRepository : IMemberPreferenceRepository
{
    private readonly HemordnaDbContext _dbContext;

    public MemberPreferenceRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public Task<MemberPreference?> FindAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken)
        => _dbContext.MemberPreferences
            .FirstOrDefaultAsync(
                preference => preference.HouseholdId == householdId && preference.MemberId == memberId,
                cancellationToken);

    public async Task AddAsync(MemberPreference preference, CancellationToken cancellationToken)
    {
        await _dbContext.MemberPreferences.AddAsync(preference, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(MemberPreference preference, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
