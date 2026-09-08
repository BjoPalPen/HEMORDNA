using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class MemberDayOffRepository : IMemberDayOffRepository
{
    private readonly HemordnaDbContext _dbContext;

    public MemberDayOffRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<MemberDayOff>> ListForHouseholdAsync(
        Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await _dbContext.MemberDaysOff
            .Where(dayOff => dayOff.HouseholdId == householdId && dayOff.Date >= from && dayOff.Date <= to)
            .ToListAsync(cancellationToken);

    public Task<MemberDayOff?> FindAsync(
        Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
        => _dbContext.MemberDaysOff.FirstOrDefaultAsync(
            dayOff => dayOff.HouseholdId == householdId && dayOff.MemberId == memberId && dayOff.Date == date,
            cancellationToken);

    public async Task AddAsync(MemberDayOff dayOff, CancellationToken cancellationToken)
    {
        await _dbContext.MemberDaysOff.AddAsync(dayOff, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
    {
        var dayOff = await _dbContext.MemberDaysOff.FirstOrDefaultAsync(
            d => d.HouseholdId == householdId && d.MemberId == memberId && d.Date == date, cancellationToken);

        if (dayOff is null)
        {
            return;
        }

        _dbContext.MemberDaysOff.Remove(dayOff);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
