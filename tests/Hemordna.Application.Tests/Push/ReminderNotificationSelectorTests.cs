using Hemordna.Application.Push;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Push;

public class ReminderNotificationSelectorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private static Reminder CreateReminder(
        DateOnly date,
        TimeOnly? timeOfDay,
        int? travelMinutes = null,
        Guid? householdId = null,
        Guid? memberId = null,
        string title = "Tandläkaren",
        string? location = "Tandvårdskliniken")
        => Reminder.Create(
            householdId ?? Guid.NewGuid(),
            memberId ?? Guid.NewGuid(),
            title,
            location,
            date,
            timeOfDay,
            CreatedAt,
            travelMinutes);

    // 2026-02-10 09:00 Stockholm time (CET, UTC+1) = 2026-02-10T08:00:00Z - outside any DST
    // transition, so a fixed +1 offset is correct here and these tests are only about the
    // window/idempotency/status rules, not the timezone math (that is DstTransitionTests below).
    private static readonly DateOnly WinterDate = new(2026, 2, 10);
    private static readonly TimeOnly WinterTimeOfDay = new(9, 0);
    private static readonly DateTimeOffset WinterAtTimeInstant = new(2026, 2, 10, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void At_time_notification_is_due_exactly_at_TimeOfDay()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay);

        var due = ReminderNotificationSelector.SelectDue(WinterAtTimeInstant, Window, [reminder]);

        var notification = Assert.Single(due);
        Assert.Equal(reminder.Id, notification.ReminderId);
        Assert.Equal(ReminderNotificationKind.AtTime, notification.Kind);
        Assert.Equal(reminder.Title, notification.Title);
        Assert.Equal(reminder.Location, notification.Location);
    }

    [Fact]
    public void Time_to_leave_is_calculated_from_travel_minutes_not_from_TimeOfDay()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay, travelMinutes: 30);
        var leaveInstant = WinterAtTimeInstant.AddMinutes(-30);

        var dueAtLeaveTime = ReminderNotificationSelector.SelectDue(leaveInstant, Window, [reminder]);
        var notification = Assert.Single(dueAtLeaveTime);
        Assert.Equal(ReminderNotificationKind.TimeToLeave, notification.Kind);

        // At TimeOfDay itself, the (already-sent, in real use) TimeToLeave instant has passed -
        // only AtTime is due now; the selector itself does not know anything was "already sent",
        // it is purely about which instants fall in the window right now.
        var dueAtTimeOfDay = ReminderNotificationSelector.SelectDue(WinterAtTimeInstant, Window, [reminder]);
        Assert.Equal(ReminderNotificationKind.AtTime, Assert.Single(dueAtTimeOfDay).Kind);
    }

    [Fact]
    public void No_travel_minutes_means_no_time_to_leave_notification_ever()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay, travelMinutes: null);

        // Scan a wide range of instants around TimeOfDay - never a TimeToLeave notification.
        for (var offsetMinutes = -120; offsetMinutes <= 120; offsetMinutes += 15)
        {
            var due = ReminderNotificationSelector.SelectDue(
                WinterAtTimeInstant.AddMinutes(offsetMinutes), Window, [reminder]);

            Assert.DoesNotContain(due, n => n.Kind == ReminderNotificationKind.TimeToLeave);
        }
    }

    [Fact]
    public void An_all_day_reminder_with_no_TimeOfDay_never_produces_a_notification()
    {
        var reminder = CreateReminder(WinterDate, timeOfDay: null);

        for (var offsetMinutes = -1440; offsetMinutes <= 1440; offsetMinutes += 120)
        {
            var due = ReminderNotificationSelector.SelectDue(
                WinterAtTimeInstant.AddMinutes(offsetMinutes), Window, [reminder]);

            Assert.Empty(due);
        }
    }

    [Fact]
    public void A_cancelled_reminder_never_produces_a_notification()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay, travelMinutes: 30);
        reminder.Cancel();

        var atLeaveTime = ReminderNotificationSelector.SelectDue(
            WinterAtTimeInstant.AddMinutes(-30), Window, [reminder]);
        var atTimeOfDay = ReminderNotificationSelector.SelectDue(WinterAtTimeInstant, Window, [reminder]);

        Assert.Empty(atLeaveTime);
        Assert.Empty(atTimeOfDay);
    }

    [Fact]
    public void An_instant_still_inside_the_window_is_due()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay);

        // 14 minutes after TimeOfDay, inside a 15-minute window.
        var due = ReminderNotificationSelector.SelectDue(
            WinterAtTimeInstant.AddMinutes(14), Window, [reminder]);

        Assert.Single(due);
    }

    [Fact]
    public void An_instant_past_the_window_is_skipped_not_sent_late()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay);

        // Exactly on the window boundary (half-open: "now - window" itself is excluded).
        var dueAtBoundary = ReminderNotificationSelector.SelectDue(
            WinterAtTimeInstant.AddMinutes(15), Window, [reminder]);
        Assert.Empty(dueAtBoundary);

        // Well past the cutoff, as after a real outage.
        var dueLongAfter = ReminderNotificationSelector.SelectDue(
            WinterAtTimeInstant.AddHours(3), Window, [reminder]);
        Assert.Empty(dueLongAfter);
    }

    [Fact]
    public void An_instant_before_TimeOfDay_is_not_yet_due()
    {
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay);

        var due = ReminderNotificationSelector.SelectDue(
            WinterAtTimeInstant.AddMinutes(-1), Window, [reminder]);

        Assert.Empty(due);
    }

    [Fact]
    public void Travel_minutes_crossing_midnight_moves_the_leave_instant_to_the_previous_day()
    {
        // 00:20 minus 60 minutes travel time is 23:20 the day before.
        var date = new DateOnly(2026, 2, 10);
        var timeOfDay = new TimeOnly(0, 20);
        var reminder = CreateReminder(date, timeOfDay, travelMinutes: 60);

        // 2026-02-09 23:20 Stockholm (CET, UTC+1) = 2026-02-09T22:20:00Z.
        var expectedLeaveInstant = new DateTimeOffset(2026, 2, 9, 22, 20, 0, TimeSpan.Zero);

        var due = ReminderNotificationSelector.SelectDue(expectedLeaveInstant, Window, [reminder]);

        var notification = Assert.Single(due);
        Assert.Equal(ReminderNotificationKind.TimeToLeave, notification.Kind);
    }

    [Fact]
    public void Household_and_member_ids_are_carried_through_to_the_notification()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var reminder = CreateReminder(WinterDate, WinterTimeOfDay, householdId: householdId, memberId: memberId);

        var notification = Assert.Single(
            ReminderNotificationSelector.SelectDue(WinterAtTimeInstant, Window, [reminder]));

        Assert.Equal(householdId, notification.HouseholdId);
        Assert.Equal(memberId, notification.MemberId);
    }

    public class DstTransitionTests
    {
        private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        // Sweden moves clocks forward on the last Sunday of March - 2026-03-29. 02:00-02:59
        // local does not exist that day (verified independently via
        // TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time").IsInvalidTime, which
        // observes the same EU DST rules as Europe/Stockholm).
        private static readonly DateOnly DayBeforeTransition = new(2026, 3, 28); // still CET, UTC+1
        private static readonly DateOnly TransitionDay = new(2026, 3, 29);       // CEST from 03:00, UTC+2

        [Fact]
        public void The_same_wall_clock_time_maps_to_a_different_UTC_instant_across_the_transition()
        {
            // Same local time of day (03:30) on both sides of the transition.
            var beforeTransition = Reminder.Create(
                Guid.NewGuid(), Guid.NewGuid(), "Före", null, DayBeforeTransition, new TimeOnly(3, 30), CreatedAt);
            var afterTransition = Reminder.Create(
                Guid.NewGuid(), Guid.NewGuid(), "Efter", null, TransitionDay, new TimeOnly(3, 30), CreatedAt);

            // Independently verified: 2026-03-28T03:30 Stockholm = 2026-03-28T02:30:00Z (CET, +1);
            // 2026-03-29T03:30 Stockholm = 2026-03-29T01:30:00Z (CEST, +2) - one hour EARLIER in
            // UTC despite being a calendar day LATER, because the offset itself changed. A naive
            // fixed-offset (or UTC-arithmetic) implementation would get this wrong.
            var correctInstantAfterTransition = new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero);
            var whatAFixedPlusOneOffsetWouldWronglyCompute = new DateTimeOffset(2026, 3, 29, 2, 30, 0, TimeSpan.Zero);

            var dueAtCorrectInstant = ReminderNotificationSelector.SelectDue(
                correctInstantAfterTransition, Window, [beforeTransition, afterTransition]);
            var dueAtWrongInstant = ReminderNotificationSelector.SelectDue(
                whatAFixedPlusOneOffsetWouldWronglyCompute, Window, [beforeTransition, afterTransition]);

            Assert.Equal(afterTransition.Id, Assert.Single(dueAtCorrectInstant).ReminderId);
            Assert.Empty(dueAtWrongInstant);
        }

        [Fact]
        public void A_TimeOfDay_inside_the_spring_forward_gap_never_becomes_due()
        {
            // 02:30 on the transition day does not exist in Stockholm time at all.
            var reminder = Reminder.Create(
                Guid.NewGuid(), Guid.NewGuid(), "Aldrig", null, TransitionDay, new TimeOnly(2, 30), CreatedAt);

            // Scan broadly around the transition - it must never be selected as due, for any now.
            var transitionInstant = new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero);
            for (var offsetMinutes = -180; offsetMinutes <= 180; offsetMinutes += 15)
            {
                var due = ReminderNotificationSelector.SelectDue(
                    transitionInstant.AddMinutes(offsetMinutes), Window, [reminder]);

                Assert.Empty(due);
            }
        }
    }
}
