using Hemordna.Domain.Reminders;

namespace Hemordna.Domain.Tests;

public class SentReminderNotificationTests
{
    private static readonly DateTimeOffset ScheduledFor = new(2026, 2, 3, 7, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SentAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_carries_the_given_household_reminder_kind_scheduled_instant_and_sent_instant()
    {
        var householdId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();

        var entry = SentReminderNotification.Create(
            householdId, reminderId, ReminderNotificationKind.AtTime, ScheduledFor, SentAt);

        Assert.Equal(householdId, entry.HouseholdId);
        Assert.Equal(reminderId, entry.ReminderId);
        Assert.Equal(ReminderNotificationKind.AtTime, entry.Kind);
        Assert.Equal(ScheduledFor, entry.ScheduledFor);
        Assert.Equal(SentAt, entry.SentAt);
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    /// <summary>ScheduledFor (what the notification was for) and SentAt (when it went out) are
    /// deliberately different instants here - the whole reason the two fields are not one.</summary>
    [Fact]
    public void ScheduledFor_and_SentAt_are_independent_of_each_other()
    {
        var entry = SentReminderNotification.Create(
            Guid.NewGuid(), Guid.NewGuid(), ReminderNotificationKind.TimeToLeave, ScheduledFor, SentAt);

        Assert.NotEqual(entry.ScheduledFor, entry.SentAt);
    }

    [Fact]
    public void An_empty_household_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SentReminderNotification.Create(
            Guid.Empty, Guid.NewGuid(), ReminderNotificationKind.TimeToLeave, ScheduledFor, SentAt));
    }

    [Fact]
    public void An_empty_reminder_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SentReminderNotification.Create(
            Guid.NewGuid(), Guid.Empty, ReminderNotificationKind.TimeToLeave, ScheduledFor, SentAt));
    }
}
