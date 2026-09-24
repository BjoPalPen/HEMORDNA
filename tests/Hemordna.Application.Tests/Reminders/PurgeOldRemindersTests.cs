using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

/// <summary>
/// A reminder is purged 30 days after its own <see cref="Reminder.Date"/> - a deliberate
/// product decision: data minimisation (CLAUDE.md §10), not disk space. No view ever shows
/// anything older than the current week, so what is left in the table is only history no
/// screen ever reads again.
/// </summary>
public class PurgeOldRemindersTests
{
    private static readonly DateOnly Today = new(2026, 2, 10);
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryReminderRepository _reminders = new();

    private PurgeOldReminders CreateUseCase() => new(_reminders);

    private static Reminder SeedReminder(
        InMemoryReminderRepository repository, DateOnly date, ReminderStatus status = ReminderStatus.Upcoming)
    {
        var reminder = Reminder.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Läkarbesök", location: null, date, timeOfDay: null, CreatedAt);

        switch (status)
        {
            case ReminderStatus.Cancelled:
                reminder.Cancel();
                break;
            case ReminderStatus.CheckedOff:
                reminder.CheckOff();
                break;
        }

        repository.Seed(reminder);
        return reminder;
    }

    [Theory]
    [InlineData(ReminderStatus.Upcoming)]
    [InlineData(ReminderStatus.Cancelled)]
    [InlineData(ReminderStatus.CheckedOff)]
    public async Task A_reminder_31_days_old_is_purged_regardless_of_status(ReminderStatus status)
    {
        var reminder = SeedReminder(_reminders, Today.AddDays(-31), status);

        var deletedCount = await CreateUseCase().HandleAsync(Today, CancellationToken.None);

        Assert.Equal(1, deletedCount);
        Assert.Null(await _reminders.FindByIdAsync(reminder.HouseholdId, reminder.Id, CancellationToken.None));
    }

    /// <summary>The boundary case: exactly 30 days old (Date == cutoff) is kept. Only strictly
    /// older than the cutoff (Date &lt; cutoff) is purged.</summary>
    [Fact]
    public async Task A_reminder_exactly_30_days_old_is_kept()
    {
        var reminder = SeedReminder(_reminders, Today.AddDays(-PurgeOldReminders.RetentionDays));

        var deletedCount = await CreateUseCase().HandleAsync(Today, CancellationToken.None);

        Assert.Equal(0, deletedCount);
        Assert.NotNull(await _reminders.FindByIdAsync(reminder.HouseholdId, reminder.Id, CancellationToken.None));
    }

    /// <summary>An appointment booked far ahead must never be touched by the purge.</summary>
    [Fact]
    public async Task A_future_reminder_is_kept()
    {
        var reminder = SeedReminder(_reminders, Today.AddDays(10));

        var deletedCount = await CreateUseCase().HandleAsync(Today, CancellationToken.None);

        Assert.Equal(0, deletedCount);
        Assert.NotNull(await _reminders.FindByIdAsync(reminder.HouseholdId, reminder.Id, CancellationToken.None));
    }

    [Fact]
    public async Task The_return_value_is_the_number_of_reminders_actually_deleted()
    {
        SeedReminder(_reminders, Today.AddDays(-40));
        SeedReminder(_reminders, Today.AddDays(-31));
        SeedReminder(_reminders, Today.AddDays(-30));
        SeedReminder(_reminders, Today);

        var deletedCount = await CreateUseCase().HandleAsync(Today, CancellationToken.None);

        Assert.Equal(2, deletedCount);
    }
}
