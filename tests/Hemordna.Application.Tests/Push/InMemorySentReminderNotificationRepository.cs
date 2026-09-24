using Hemordna.Application.Push;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Push;

internal sealed class InMemorySentReminderNotificationRepository : ISentReminderNotificationRepository
{
    private readonly HashSet<(Guid ReminderId, ReminderNotificationKind Kind, DateTimeOffset ScheduledFor)> _sent = [];

    internal int MarkSentCallCount { get; private set; }

    public Task<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind, DateTimeOffset ScheduledFor)>> ListSentAsync(
        IReadOnlyCollection<Guid> reminderIds, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlySet<(Guid ReminderId, ReminderNotificationKind Kind, DateTimeOffset ScheduledFor)>>(
            _sent.Where(entry => reminderIds.Contains(entry.ReminderId)).ToHashSet());

    public Task MarkSentAsync(
        Guid householdId,
        Guid reminderId,
        ReminderNotificationKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        MarkSentCallCount++;

        // A duplicate call is exactly the bug idempotency guards against - fail the same way the
        // real unique index (SentReminderNotificationConfiguration) would.
        if (!_sent.Add((reminderId, kind, scheduledFor)))
        {
            throw new InvalidOperationException(
                $"Reminder {reminderId} notification {kind} scheduled for {scheduledFor} was already marked sent.");
        }

        return Task.CompletedTask;
    }
}
