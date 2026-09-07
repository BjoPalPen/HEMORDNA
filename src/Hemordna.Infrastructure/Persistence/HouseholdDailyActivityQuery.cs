using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class HouseholdDailyActivityQuery : IHouseholdDailyActivityQuery
{
    private readonly HemordnaDbContext _dbContext;

    public HouseholdDailyActivityQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<DailyActivitySummary>> FindRecentDaysAsync(
        Guid householdId,
        DateOnly today,
        int days,
        CancellationToken cancellationToken)
    {
        var since = today.AddDays(-(days - 1));

        var counted = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.ScheduledDate >= since
                && occurrence.ScheduledDate <= today)
            .GroupBy(occurrence => occurrence.ScheduledDate)
            .Select(group => new
            {
                Date = group.Key,
                // A skipped occurrence ("not needed this time") is a conscious decision to shrink
                // the day's scope, not an unfinished item - it must not sit in the total forever
                // capping the ring below 100% no matter what still gets done.
                Total = group.Count(occurrence => occurrence.Status != TaskOccurrenceStatus.Skipped),
                Completed = group.Count(occurrence => occurrence.Status == TaskOccurrenceStatus.Completed)
            })
            .ToDictionaryAsync(row => row.Date, cancellationToken);

        return Enumerable.Range(0, days)
            .Select(offset => since.AddDays(offset))
            .Select(date => counted.TryGetValue(date, out var row)
                ? new DailyActivitySummary(date, row.Completed, row.Total)
                : new DailyActivitySummary(date, 0, 0))
            .ToList();
    }
}
