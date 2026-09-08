using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class MemberTimeCreditRepository : IMemberTimeCreditRepository
{
    private readonly HemordnaDbContext _dbContext;

    public MemberTimeCreditRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<MemberTimeCredit>> ListForMemberAsync(
        Guid householdId, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => await _dbContext.MemberTimeCredits
            .Where(entry => entry.HouseholdId == householdId && entry.MemberId == memberId
                && entry.OccurredOn >= from && entry.OccurredOn <= to)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MemberTimeCredit entry, CancellationToken cancellationToken)
    {
        await _dbContext.MemberTimeCredits.AddAsync(entry, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveByOccurrenceAsync(
        Guid occurrenceId, IReadOnlyCollection<TimeCreditReason> reasons, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.MemberTimeCredits
            .Where(entry => entry.OccurrenceId == occurrenceId && reasons.Contains(entry.Reason))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return;
        }

        _dbContext.MemberTimeCredits.RemoveRange(rows);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
