using Hemordna.Application.Push;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class SentReminderNotificationRepository : ISentReminderNotificationRepository
{
    private readonly HemordnaDbContext _dbContext;

    public SentReminderNotificationRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind, DateTimeOffset ScheduledFor)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken)
    {
        if (reminderIds.Count == 0)
        {
            return new HashSet<(Guid, ReminderNotificationKind, DateTimeOffset)>();
        }

        var sent = await _dbContext.SentReminderNotifications
            .AsNoTracking()
            .Where(entry => reminderIds.Contains(entry.ReminderId))
            .Select(entry => new { entry.ReminderId, entry.Kind, entry.ScheduledFor })
            .ToListAsync(cancellationToken);

        return sent.Select(entry => (entry.ReminderId, entry.Kind, entry.ScheduledFor)).ToHashSet();
    }

    public async Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await _dbContext.SentReminderNotifications.AddAsync(
            SentReminderNotification.Create(householdId, reminderId, kind, scheduledFor, sentAt),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
