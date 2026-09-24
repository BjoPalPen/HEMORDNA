using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class ReminderRepository : IReminderRepository
{
    private readonly HemordnaDbContext _dbContext;

    public ReminderRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        await _dbContext.Reminders.AddAsync(reminder, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Reminder?> FindByIdAsync(Guid householdId, Guid reminderId, CancellationToken cancellationToken)
        => _dbContext.Reminders
            .FirstOrDefaultAsync(
                reminder => reminder.HouseholdId == householdId && reminder.Id == reminderId,
                cancellationToken);

    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<Reminder>> ListForMemberInRangeAsync(
        Guid householdId,
        Guid memberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
        => await _dbContext.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.HouseholdId == householdId
                && reminder.MemberId == memberId
                && reminder.Status != ReminderStatus.Cancelled
                && reminder.Date >= fromDate
                && reminder.Date <= toDate)
            .OrderBy(reminder => reminder.Date)
            .ThenBy(reminder => reminder.TimeOfDay)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Reminder>> ListInDateRangeAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => await _dbContext.Reminders
            .AsNoTracking()
            .Where(reminder => reminder.Date >= fromDate && reminder.Date <= toDate)
            .ToListAsync(cancellationToken);

    // The existing (HouseholdId, MemberId, Date) index (see ReminderConfiguration) does not help
    // this query - it filters on Date alone, across every household and member. Deliberately not
    // adding an index for it: the table is small and this query runs once a day (the reminder
    // cleanup background service), not on a user-facing path.
    public Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken cancellationToken)
        => _dbContext.Reminders
            .Where(reminder => reminder.Date < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
}
