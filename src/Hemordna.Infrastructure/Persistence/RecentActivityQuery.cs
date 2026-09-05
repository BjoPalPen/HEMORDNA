using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class RecentActivityQuery : IRecentActivityQuery
{
    private readonly HemordnaDbContext _dbContext;

    public RecentActivityQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<RecentActivity>> FindRecentlyCompletedAsync(
        Guid householdId,
        int limit,
        CancellationToken cancellationToken)
    {
        // Ordered and limited before projecting into RecentActivity: EF Core cannot translate
        // an OrderBy over a property re-read off a freshly constructed record type, so the
        // record is built in memory after the query itself has already run in the database.
        var rows = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.Status == TaskOccurrenceStatus.Completed
                && occurrence.CompletedByMemberId != null
                && occurrence.CompletedAt != null)
            .Join(
                _dbContext.TaskDefinitions.AsNoTracking(),
                occurrence => occurrence.TaskDefinitionId,
                definition => definition.Id,
                (occurrence, definition) => new { occurrence, definition.Name })
            .Join(
                _dbContext.HouseholdMembers.AsNoTracking(),
                row => row.occurrence.CompletedByMemberId,
                member => member.Id,
                (row, member) => new { row.occurrence, row.Name, member.DisplayName })
            .OrderByDescending(row => row.occurrence.CompletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new RecentActivity(
            row.occurrence.Id, row.Name, row.DisplayName, row.occurrence.CompletedAt!.Value))];
    }
}
