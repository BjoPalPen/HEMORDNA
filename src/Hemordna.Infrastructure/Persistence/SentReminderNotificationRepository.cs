using Hemordna.Application.Push;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class SentReminderNotificationRepository : ISentReminderNotificationRepository
{
    private readonly HemordnaDbContext _dbContext;

    public SentReminderNotificationRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken)
    {
        if (reminderIds.Count == 0)
        {
            return new HashSet<(Guid, ReminderNotificationKind)>();
        }

        var sent = await _dbContext.SentReminderNotifications
            .AsNoTracking()
            .Where(entry => reminderIds.Contains(entry.ReminderId))
            .Select(entry => new { entry.ReminderId, entry.Kind })
            .ToListAsync(cancellationToken);

        return sent.Select(entry => (entry.ReminderId, entry.Kind)).ToHashSet();
    }

    public async Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        // Interim state for this commit only: ISentReminderNotificationRepository.MarkSentAsync
        // does not yet receive the notification's real due instant (ScheduledFor) - that plumbing
        // (DueReminderNotification, ListSentAsync, this method's signature) lands in the next
        // commit. Passing sentAt for both keeps this compiling without pretending the two-field
        // idempotency key is wired end-to-end yet; nothing depends on ScheduledFor being correct
        // until that commit lands.
        await _dbContext.SentReminderNotifications.AddAsync(
            SentReminderNotification.Create(householdId, reminderId, kind, scheduledFor: sentAt, sentAt),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
