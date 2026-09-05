using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class WeeklyStatusQuery : IWeeklyStatusQuery
{
    private readonly HemordnaDbContext _dbContext;

    public WeeklyStatusQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<MemberDayStatus>> FindWeeklyStatusAsync(
        Guid householdId,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        var weekEnd = weekStart.AddDays(7);

        var rows = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.AssignedMemberId != null
                && occurrence.ScheduledDate >= weekStart
                && occurrence.ScheduledDate < weekEnd
                && occurrence.Status != TaskOccurrenceStatus.Skipped)
            .GroupBy(occurrence => new { MemberId = occurrence.AssignedMemberId!.Value, occurrence.ScheduledDate })
            .Select(group => new
            {
                group.Key.MemberId,
                group.Key.ScheduledDate,
                AllCompleted = group.All(occurrence => occurrence.Status == TaskOccurrenceStatus.Completed)
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new MemberDayStatus(
            row.MemberId, row.ScheduledDate, row.AllCompleted ? DayStatus.Done : DayStatus.Planned))];
    }
}
