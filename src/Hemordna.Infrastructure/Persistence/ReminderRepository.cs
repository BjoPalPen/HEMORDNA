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
}
