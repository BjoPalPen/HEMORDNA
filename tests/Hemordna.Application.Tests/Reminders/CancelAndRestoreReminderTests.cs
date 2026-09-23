using Hemordna.Application.Reminders;
using Hemordna.Domain.Common;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class CancelAndRestoreReminderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private CancelReminder Cancel() => new(_reminders);

    private RestoreReminder Restore() => new(_reminders);

    private LapseReminder Lapse() => new(_reminders);

    private Reminder Seed(Guid? memberId = null)
    {
        var reminder = Reminder.Create(
            HouseholdId, memberId ?? AnnaId, "Tandläkare", null, Friday, null, CreatedAt);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Cancels_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await Cancel().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderStatus.Cancelled, result.Status);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_cannot_be_cancelled()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await Cancel().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_cancel()
    {
        var result = await Cancel().HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Restores_the_callers_own_cancelled_reminder()
    {
        var reminder = Seed();
        reminder.Cancel();

        var result = await Restore().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderStatus.Upcoming, result.Status);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_cancelled_reminder_cannot_be_restored()
    {
        var reminder = Seed(memberId: BjornId);
        reminder.Cancel();

        var result = await Restore().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_restore()
    {
        var result = await Restore().HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Restoring_a_reminder_that_is_not_cancelled_throws_uncaught()
    {
        var reminder = Seed();

        await Assert.ThrowsAsync<DomainException>(
            () => Restore().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Lapses_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await Lapse().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderStatus.Lapsed, result.Status);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_cannot_be_lapsed()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await Lapse().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_lapse()
    {
        var result = await Lapse().HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Restores_the_callers_own_lapsed_reminder()
    {
        var reminder = Seed();
        reminder.Lapse();

        var result = await Restore().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderStatus.Upcoming, result.Status);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_lapsed_reminder_cannot_be_restored()
    {
        var reminder = Seed(memberId: BjornId);
        reminder.Lapse();

        var result = await Restore().HandleAsync(HouseholdId, AnnaId, reminder.Id, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(ReminderStatus.Lapsed, reminder.Status);
    }
}
