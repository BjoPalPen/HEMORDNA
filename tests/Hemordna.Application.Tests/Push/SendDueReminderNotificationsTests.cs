using Hemordna.Application.Push;
using Hemordna.Application.Tests.Reminders;
using Hemordna.Domain.Push;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Push;

public class SendDueReminderNotificationsTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // 2026-02-10 09:00 Stockholm (CET, UTC+1) = 2026-02-10T08:00:00Z - outside any DST
    // transition, so this suite is only about idempotency/orchestration, not timezone math
    // (ReminderNotificationSelectorTests.DstTransitionTests owns that).
    private static readonly DateOnly Date = new(2026, 2, 10);
    private static readonly TimeOnly TimeOfDay = new(9, 0);
    private static readonly DateTimeOffset AtTimeInstant = new(2026, 2, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryReminderRepository _reminders = new();
    private readonly InMemorySentReminderNotificationRepository _sentLog = new();
    private readonly InMemoryPushSubscriptionRepository _subscriptions = new();
    private readonly FakePushSender _sender = new();

    private SendDueReminderNotifications CreateUseCase()
        => new(_reminders, _sentLog, _subscriptions, _sender);

    /// <summary>
    /// Björn's decision, and the reason the mark-sent write sits AFTER the send: a notification
    /// that reached no device must be tried again while the window is open, not quietly written
    /// off as delivered. Fails if anyone reorders the two back to mark-then-send - which is the
    /// whole point of this test existing, because both orderings look equally reasonable in a
    /// diff and only one of them keeps a "Dags att gå" from vanishing when Apple hiccups.
    /// </summary>
    [Fact]
    public async Task A_notification_that_reached_no_device_is_not_recorded_as_sent()
    {
        SeedDueReminder();
        _sender.SentCount = 0;

        var delivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(0, _sentLog.MarkSentCallCount);
    }

    /// <summary>The same sweep run twice still sends once - at-least-once must not become
    /// every-time.</summary>
    [Fact]
    public async Task A_delivered_notification_is_recorded_and_not_sent_again()
    {
        SeedDueReminder();

        await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);
        var second = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }

    private (Reminder Reminder, Guid HouseholdId, Guid MemberId) SeedDueReminder()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reminder = Reminder.Create(
            householdId, memberId, "Tandläkaren", "Tandvårdskliniken", Date, TimeOfDay, CreatedAt);
        _reminders.Seed(reminder);
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", CreatedAt));
        _sender.SentCount = 1;

        return (reminder, householdId, memberId);
    }

    [Fact]
    public async Task A_due_notification_is_sent_once()
    {
        SeedDueReminder();

        var delivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
        Assert.Equal("Tandläkaren", _sender.LastTitle);
    }

    /// <summary>
    /// This follows directly from ReminderNotificationSelector.PrepareMinutes: TimeToLeave can
    /// now arrive up to 5 minutes before the reminder's own TimeOfDay (and later still, inside
    /// SendDueReminderNotifications.DueWindow), so a body that just said "Dags att gå" would be
    /// wrong whenever it's read a few minutes after it lands. The departure time in the body
    /// keeps it correct regardless of when it's actually read.
    /// </summary>
    [Fact]
    public async Task A_time_to_leave_notification_body_carries_the_departure_time()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reminder = Reminder.Create(
            householdId, memberId, "Läkarbesök", null, Date, TimeOfDay, CreatedAt, travelMinutes: 25);
        _reminders.Seed(reminder);
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", CreatedAt));
        _sender.SentCount = 1;

        // The notification itself fires at TimeOfDay 09:00 minus (25 travel + 5 PrepareMinutes =)
        // 30 minutes = 08:30 Stockholm (CET, UTC+1) = 07:30Z - but the real departure time it
        // must NAME in its text is TimeOfDay minus travel minutes ONLY: 09:00 - 25 = 08:35. The
        // whole point of PrepareMinutes is that these two clock times are 5 minutes apart.
        var notifyInstant = new DateTimeOffset(2026, 2, 10, 7, 30, 0, TimeSpan.Zero);

        var delivered = await CreateUseCase().HandleAsync(notifyInstant, CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Equal("Dags att gå", _sender.LastTitle);
        Assert.Equal("Läkarbesök · gå 08:35", _sender.LastBody);
    }

    /// <summary>Same as above, but with a location set - the departure time is appended after
    /// the existing title/location text, not instead of it.</summary>
    [Fact]
    public async Task A_time_to_leave_notification_body_carries_both_location_and_departure_time()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reminder = Reminder.Create(
            householdId, memberId, "Läkarbesök", "Vårdcentralen", Date, TimeOfDay, CreatedAt, travelMinutes: 25);
        _reminders.Seed(reminder);
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", CreatedAt));
        _sender.SentCount = 1;

        // Same instant/departure split as the test above: notified at 07:30Z, real departure
        // 08:35 Stockholm.
        var notifyInstant = new DateTimeOffset(2026, 2, 10, 7, 30, 0, TimeSpan.Zero);

        var delivered = await CreateUseCase().HandleAsync(notifyInstant, CancellationToken.None);

        Assert.Equal(1, delivered);
        Assert.Equal("Dags att gå", _sender.LastTitle);
        Assert.Equal("Läkarbesök – Vårdcentralen · gå 08:35", _sender.LastBody);
    }

    [Fact]
    public async Task The_next_sweep_does_not_send_the_same_notification_again()
    {
        SeedDueReminder();
        var useCase = CreateUseCase();

        var firstSweep = await useCase.HandleAsync(AtTimeInstant, CancellationToken.None);
        // A later sweep, still inside the due window - simulates the background service's next
        // poll finding the same reminder still a due candidate.
        var secondSweep = await useCase.HandleAsync(AtTimeInstant.AddMinutes(5), CancellationToken.None);

        Assert.Equal(1, firstSweep);
        Assert.Equal(0, secondSweep);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }

    [Fact]
    public async Task Restarting_mid_sweep_does_not_double_send()
    {
        // Simulates a server restart: a brand-new use case instance (as a new DI scope would
        // create), backed by the SAME persisted sent-log and reminder data, sweeping again.
        SeedDueReminder();

        var firstDelivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);
        var secondDelivered = await CreateUseCase().HandleAsync(AtTimeInstant.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, firstDelivered);
        Assert.Equal(0, secondDelivered);
        Assert.Equal(1, _sentLog.MarkSentCallCount);
    }

    [Fact]
    public async Task A_notification_past_the_cutoff_is_never_sent()
    {
        SeedDueReminder();

        var delivered = await CreateUseCase().HandleAsync(
            AtTimeInstant + SendDueReminderNotifications.DueWindow + TimeSpan.FromMinutes(1),
            CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(0, _sentLog.MarkSentCallCount);
    }

    [Fact]
    public async Task An_all_day_reminder_is_never_sent()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _reminders.Seed(Reminder.Create(
            householdId, memberId, "Städdag", null, Date, timeOfDay: null, CreatedAt));
        _subscriptions.Seed(PushSubscription.Subscribe(
            householdId, memberId, "https://push.example.com/a", "p256dh-key", "auth-secret", CreatedAt));

        var delivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(0, _sentLog.MarkSentCallCount);
    }

    [Fact]
    public async Task A_cancelled_reminder_is_never_sent()
    {
        var (reminder, _, _) = SeedDueReminder();
        reminder.Cancel();

        var delivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(0, _sentLog.MarkSentCallCount);
    }

    [Fact]
    public async Task With_no_live_subscriptions_nothing_is_marked_sent()
    {
        // No seeded PushSubscription - the member has never granted permission.
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        _reminders.Seed(Reminder.Create(
            householdId, memberId, "Tandläkaren", null, Date, TimeOfDay, CreatedAt));

        var delivered = await CreateUseCase().HandleAsync(AtTimeInstant, CancellationToken.None);

        Assert.Equal(0, delivered);
        Assert.Equal(0, _sentLog.MarkSentCallCount);
    }
}
