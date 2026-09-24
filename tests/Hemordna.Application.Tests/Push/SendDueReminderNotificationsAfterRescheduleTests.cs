using Hemordna.Application.Push;
using Hemordna.Application.Reminders;
using Hemordna.Application.Tests.Reminders;
using Hemordna.Domain.Push;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Push;

/// <summary>
/// Covers the bug this design exists to fix: a reminder whose notification already fired, then
/// moved (or re-timed) to a new instant, must be notified again at the new instant - and a
/// reminder edited WITHOUT its computed time actually changing must not be notified again. See
/// docs/PRODUCT.md §11 and <c>ISentReminderNotificationRepository</c>'s remarks: the fix is
/// entirely that <c>ScheduledFor</c> is part of the idempotency key, so <see cref="MoveReminder"/>
/// and <see cref="SetReminderTravelMinutes"/> need no special knowledge of push notifications at
/// all - they are exercised here completely unchanged.
/// </summary>
public class SendDueReminderNotificationsAfterRescheduleTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // 2026-02-10, Stockholm (CET, UTC+1) - outside any DST transition, same convention as
    // SendDueReminderNotificationsTests: this suite is about the idempotency key, not timezone math.
    private static readonly DateOnly Date = new(2026, 2, 10);
    private static readonly TimeOnly TimeOfDay = new(9, 0);
    private static readonly DateTimeOffset AtTimeInstant = new(2026, 2, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryReminderRepository _reminders = new();
    private readonly InMemorySentReminderNotificationRepository _sentLog = new();
    private readonly InMemoryPushSubscriptionRepository _subscriptions = new();
    private readonly FakePushSender _sender = new();

    private SendDueReminderNotifications CreateSendUseCase() => new(_reminders, _sentLog, _subscriptions, _sender);

    private MoveReminder CreateMoveUseCase() => new(_reminders);

    private SetReminderTravelMinutes CreateTravelMinutesUseCase() => new(_reminders);

    private (Reminder Reminder, Guid HouseholdId, Guid MemberId) SeedDueReminder(int? travelMinutes = null)
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reminder = Reminder.Create(
            householdId, memberId, "Tandläkaren", "Tandvårdskliniken", Date, TimeOfDay, CreatedAt, travelMinutes);
        _reminders.Seed(reminder);
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", CreatedAt));
        _sender.SentCount = 1;

        return (reminder, householdId, memberId);
    }

    [Fact]
    public async Task A_reminder_moved_to_a_new_time_after_its_AtTime_notification_was_sent_is_notified_again()
    {
        var (reminder, householdId, memberId) = SeedDueReminder();
        var sendUseCase = CreateSendUseCase();

        var firstDelivered = await sendUseCase.HandleAsync(AtTimeInstant, CancellationToken.None);
        Assert.Equal(1, firstDelivered);

        // The doctor's office calls and moves the appointment three hours later, same day.
        var newTimeOfDay = new TimeOnly(12, 0);
        var moved = await CreateMoveUseCase().HandleAsync(
            householdId, memberId, reminder.Id, Date, newTimeOfDay, CancellationToken.None);
        Assert.NotNull(moved);

        // 12:00 Stockholm (CET, UTC+1) = 11:00Z.
        var newAtTimeInstant = new DateTimeOffset(2026, 2, 10, 11, 0, 0, TimeSpan.Zero);
        var secondDelivered = await sendUseCase.HandleAsync(newAtTimeInstant, CancellationToken.None);

        Assert.Equal(1, secondDelivered);
    }

    [Fact]
    public async Task A_reminder_whose_travel_time_changes_after_TimeToLeave_was_sent_is_notified_again_at_the_new_leave_time()
    {
        var (reminder, householdId, memberId) = SeedDueReminder(travelMinutes: 30);
        var sendUseCase = CreateSendUseCase();

        // 09:00 minus 30 minutes = 08:30 Stockholm = 07:30Z.
        var firstLeaveInstant = new DateTimeOffset(2026, 2, 10, 7, 30, 0, TimeSpan.Zero);
        var firstDelivered = await sendUseCase.HandleAsync(firstLeaveInstant, CancellationToken.None);
        Assert.Equal(1, firstDelivered);

        var moved = await CreateTravelMinutesUseCase().HandleAsync(
            householdId, memberId, reminder.Id, travelMinutes: 60, CancellationToken.None);
        Assert.NotNull(moved);

        // 09:00 minus 60 minutes = 08:00 Stockholm = 07:00Z.
        var newLeaveInstant = new DateTimeOffset(2026, 2, 10, 7, 0, 0, TimeSpan.Zero);
        var secondDelivered = await sendUseCase.HandleAsync(newLeaveInstant, CancellationToken.None);

        Assert.Equal(1, secondDelivered);
    }

    /// <summary>
    /// The regression this whole design protects against: a title edit on Min dag also re-runs
    /// MoveReminder with the reminder's own unchanged date/time (see MinDag.razor's
    /// SaveReminderAsync). If that were ever mistaken for a real move, every field edit would
    /// resend every notification - a worse bug than the one being fixed. Here, nothing needs to
    /// know it "did nothing": the computed instant is identical, so it is still the same
    /// (ReminderId, Kind, ScheduledFor) key already recorded as sent.
    /// </summary>
    [Fact]
    public async Task Moving_a_reminder_to_the_same_date_and_time_does_not_resend_its_notification()
    {
        var (reminder, householdId, memberId) = SeedDueReminder();
        var sendUseCase = CreateSendUseCase();

        var firstDelivered = await sendUseCase.HandleAsync(AtTimeInstant, CancellationToken.None);
        Assert.Equal(1, firstDelivered);

        var moved = await CreateMoveUseCase().HandleAsync(
            householdId, memberId, reminder.Id, Date, TimeOfDay, CancellationToken.None);
        Assert.NotNull(moved);

        // Still within DueWindow of the same due instant.
        var secondDelivered = await sendUseCase.HandleAsync(
            AtTimeInstant.AddMinutes(5), CancellationToken.None);

        Assert.Equal(0, secondDelivered);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }

    /// <summary>The same regression as above, for the other field that can move a due instant.</summary>
    [Fact]
    public async Task Setting_the_same_travel_minutes_again_does_not_resend_TimeToLeave()
    {
        var (reminder, householdId, memberId) = SeedDueReminder(travelMinutes: 30);
        var sendUseCase = CreateSendUseCase();

        var leaveInstant = new DateTimeOffset(2026, 2, 10, 7, 30, 0, TimeSpan.Zero);
        var firstDelivered = await sendUseCase.HandleAsync(leaveInstant, CancellationToken.None);
        Assert.Equal(1, firstDelivered);

        var updated = await CreateTravelMinutesUseCase().HandleAsync(
            householdId, memberId, reminder.Id, travelMinutes: 30, CancellationToken.None);
        Assert.NotNull(updated);

        var secondDelivered = await sendUseCase.HandleAsync(
            leaveInstant.AddMinutes(5), CancellationToken.None);

        Assert.Equal(0, secondDelivered);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }

    /// <summary>
    /// MoveTo(date, null) - moving to "all day" - clears TravelMinutes as a domain side effect
    /// (Reminder.MoveTo), which is itself a change to when "dags att gå" would have been. Under
    /// this design nothing needs to specially detect that: TimeToLeave simply stops being
    /// produced at all once TravelMinutes is null, so the old sent record is inert, not a stale
    /// lock - no exception, no notification, for either kind, ever again for this reminder.
    /// </summary>
    [Fact]
    public async Task Clearing_the_time_of_day_stops_both_notifications_even_after_they_were_already_sent()
    {
        var (reminder, householdId, memberId) = SeedDueReminder(travelMinutes: 30);
        var sendUseCase = CreateSendUseCase();

        var leaveInstant = new DateTimeOffset(2026, 2, 10, 7, 30, 0, TimeSpan.Zero);
        Assert.Equal(1, await sendUseCase.HandleAsync(leaveInstant, CancellationToken.None));
        Assert.Equal(1, await sendUseCase.HandleAsync(AtTimeInstant, CancellationToken.None));

        var moved = await CreateMoveUseCase().HandleAsync(
            householdId, memberId, reminder.Id, Date, timeOfDay: null, CancellationToken.None);
        Assert.NotNull(moved);
        Assert.Null(moved.TimeOfDay);
        Assert.Null(moved.TravelMinutes);

        var afterLeaveInstant = await sendUseCase.HandleAsync(leaveInstant.AddMinutes(1), CancellationToken.None);
        var afterAtTimeInstant = await sendUseCase.HandleAsync(AtTimeInstant.AddMinutes(1), CancellationToken.None);

        Assert.Equal(0, afterLeaveInstant);
        Assert.Equal(0, afterAtTimeInstant);
    }

    /// <summary>Idempotency still holds under the new (ReminderId, Kind, ScheduledFor) key for a
    /// reminder nobody touched at all - two sweeps in a row inside DueWindow still send once.</summary>
    [Fact]
    public async Task Two_sweeps_for_an_unchanged_reminder_still_send_only_once()
    {
        SeedDueReminder();
        var sendUseCase = CreateSendUseCase();

        var firstDelivered = await sendUseCase.HandleAsync(AtTimeInstant, CancellationToken.None);
        var secondDelivered = await sendUseCase.HandleAsync(AtTimeInstant.AddMinutes(5), CancellationToken.None);

        Assert.Equal(1, firstDelivered);
        Assert.Equal(0, secondDelivered);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }
}
