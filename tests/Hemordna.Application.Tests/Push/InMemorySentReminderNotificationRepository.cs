using Hemordna.Application.Push;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Push;

internal sealed class InMemorySentReminderNotificationRepository : ISentReminderNotificationRepository
{
    private readonly HashSet<(Guid ReminderId, ReminderNotificationKind Kind)> _sent = [];

    internal int MarkSentCallCount { get; private set; }

    public Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind)>>(
            _sent.Where(entry => reminderIds.Contains(entry.ReminderId)).ToHashSet());

    public Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        MarkSentCallCount++;

        // A duplicate call is exactly the bug idempotency guards against - fail the same way the
        // real unique index (SentReminderNotificationConfiguration) would.
        if (!_sent.Add((reminderId, kind)))
        {
            throw new InvalidOperationException(
                $"Reminder {reminderId} notification {kind} was already marked sent.");
        }

        return Task.CompletedTask;
    }
}
