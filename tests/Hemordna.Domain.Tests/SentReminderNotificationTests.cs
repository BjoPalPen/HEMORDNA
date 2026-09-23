using Hemordna.Domain.Reminders;

namespace Hemordna.Domain.Tests;

public class SentReminderNotificationTests
{
    private static readonly DateTimeOffset SentAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_carries_the_given_household_reminder_kind_and_instant()
    {
        var householdId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var entry = SentReminderNotification.Create(
            householdId, reminderId, ReminderNotificationKind.AtTime, SentAt);

        Assert.Equal(householdId, entry.HouseholdId);
        Assert.Equal(reminderId, entry.ReminderId);
        Assert.Equal(ReminderNotificationKind.AtTime, entry.Kind);
        Assert.Equal(SentAt, entry.SentAt);
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public void An_empty_household_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SentReminderNotification.Create(
            Guid.Empty, Guid.NewGuid(), ReminderNotificationKind.TimeToLeave, SentAt));
    }

    [Fact]
    public void An_empty_reminder_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SentReminderNotification.Create(
            Guid.NewGuid(), Guid.Empty, ReminderNotificationKind.TimeToLeave, SentAt));
    }
}
