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

        var daysOff = await _dbContext.MemberDaysOff
            .AsNoTracking()
            .Where(dayOff => dayOff.HouseholdId == householdId
                && dayOff.Date >= weekStart
                && dayOff.Date < weekEnd)
            .Select(dayOff => new { dayOff.MemberId, dayOff.Date })
            .ToListAsync(cancellationToken);

        var daysOffKeys = daysOff.Select(dayOff => (dayOff.MemberId, dayOff.Date)).ToHashSet();

        var statusByKey = rows.ToDictionary(
            row => (MemberId: row.MemberId, Date: row.ScheduledDate),
            row => row.AllCompleted ? DayStatus.Done : DayStatus.Planned);

        // A day off needs its own row even when nothing was ever scheduled that day - taking the
        // day off does not require a plan to have existed first (e.g. it was cleared by
        // SetMemberDayOff.DeferAll, or the member never had anything due).
        var keys = statusByKey.Keys.Concat(daysOffKeys).Distinct();

        return [.. keys.Select(key => new MemberDayStatus(
            key.MemberId,
            key.Date,
            statusByKey.GetValueOrDefault(key, DayStatus.NoPlan),
            daysOffKeys.Contains(key)))];
    }
}
