using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

internal sealed class InMemoryReminderRepository : IReminderRepository
{
    private readonly List<Reminder> _reminders = [];

    internal int AddCallCount { get; private set; }

    internal int UpdateCallCount { get; private set; }

    internal void Seed(Reminder reminder) => _reminders.Add(reminder);

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _reminders.Add(reminder);
        return Task.CompletedTask;
    }

    public Task<Reminder?> FindByIdAsync(Guid householdId, Guid reminderId, CancellationToken cancellationToken)
        => Task.FromResult(_reminders.FirstOrDefault(reminder =>
            reminder.HouseholdId == householdId && reminder.Id == reminderId));

    public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Reminder>> ListForMemberInRangeAsync(
        Guid householdId,
        Guid memberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Reminder>>([.. _reminders
            .Where(reminder => reminder.HouseholdId == householdId
                && reminder.MemberId == memberId
                && reminder.Status != ReminderStatus.Cancelled
                && reminder.Date >= fromDate
                && reminder.Date <= toDate)
            .OrderBy(reminder => reminder.Date)
            .ThenBy(reminder => reminder.TimeOfDay)]);

    public Task<IReadOnlyList<Reminder>> ListInDateRangeAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Reminder>>([.. _reminders
            .Where(reminder => reminder.Date >= fromDate && reminder.Date <= toDate)]);

    public Task<int> DeleteOlderThanAsync(DateOnly cutoff, CancellationToken cancellationToken)
    {
        var removed = _reminders.RemoveAll(reminder => reminder.Date < cutoff);
        return Task.FromResult(removed);
    }
}
