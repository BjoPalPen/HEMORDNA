using Hemordna.Application.Time;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Push;

/// <summary>
/// Picks which reminder push notifications are due at a given instant. A pure, deterministic
/// function of its inputs - no database, no clock (CLAUDE.md §5): whoever calls this reads
/// <see cref="TimeProvider"/> once and hands the result in as <c>now</c>, this only compares.
/// That is what makes the notification rules - including the daylight-saving behaviour - testable
/// without a database and without waiting for March; see ReminderNotificationSelectorTests.
/// </summary>
/// <remarks>
/// <para>
/// Two notifications per reminder, never more (docs/PRODUCT.md §11, this task's spec):
/// <see cref="ReminderNotificationKind.TimeToLeave"/> at <c>TimeOfDay</c> minus
/// <c>TravelMinutes</c> minus <see cref="PrepareMinutes"/> (only when <c>TravelMinutes</c> is
/// set), and <see cref="ReminderNotificationKind.AtTime"/> at <c>TimeOfDay</c> itself. A reminder with no
/// <c>TimeOfDay</c> ("all day"), or a reminder that is not <see cref="ReminderStatus.Upcoming"/> -
/// cancelled OR checked off (<see cref="ReminderStatus.CheckedOff"/>) - never produces either. A
/// checked-off reminder muting its own remaining notices is the whole point of the status: if
/// someone has already said "I don't need reminding about this", a "time to leave" or "at time"
/// notification arriving afterwards would flatly contradict them.
/// </para>
/// <para>
/// <b>"Due" means the notification's instant falls in the half-open window
/// (<paramref name="now"/> - <paramref name="window"/>, <paramref name="now"/>]</b> - strictly
/// past instants are excluded by the lower bound (the cutoff: after an outage, a notification
/// whose moment is long gone is skipped rather than sent late, so a delayed arrival never reads
/// as "you missed this" - CLAUDE.md §8) and not-yet-due instants are excluded by the upper bound.
/// <paramref name="window"/> must be at least as wide as the background service's poll interval,
/// or an instant can fall between two sweeps and never be seen as due at all; see
/// <c>SendDueReminderNotifications.DueWindow</c> for the chosen value and its reasoning.
/// </para>
/// </remarks>
public static class ReminderNotificationSelector
{
    /// <summary>
    /// Extra minutes subtracted on top of <c>TravelMinutes</c> when computing
    /// <see cref="ReminderNotificationKind.TimeToLeave"/> - Björn's decision: the notification is
    /// a "get ready" signal, not a "walk out the door now" signal, and getting dressed / finding
    /// keys takes a few minutes that <c>TravelMinutes</c> itself does not cover (that field is
    /// purely how long the trip takes, not how long it takes to become ready to make it). This is
    /// what actually makes it NOT zero: without it, "TimeToLeave" would fire at the instant a
    /// member needs to already be walking out the door, leaving no time to act on it at all.
    /// </summary>
    public const int PrepareMinutes = 5;

    public static IReadOnlyList<DueReminderNotification> SelectDue(
        DateTimeOffset now, TimeSpan window, IReadOnlyList<Reminder> reminders)
    {
        ArgumentNullException.ThrowIfNull(reminders);

        if (window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must not be negative.");
        }

        List<DueReminderNotification>? due = null;

        foreach (var reminder in reminders)
        {
            // Anything other than Upcoming - cancelled or checked off (CheckedOff) alike - has
            // nothing left to notify about. Written as "not Upcoming" rather than naming each
            // non-upcoming status so a future status added here is silent by default, the same
            // fail-safe direction as the rest of this selector.
            if (reminder.Status != ReminderStatus.Upcoming || reminder.TimeOfDay is not { } timeOfDay)
            {
                continue;
            }

            TryAdd(
                reminder, ReminderNotificationKind.AtTime, reminder.Date, timeOfDay, now, window,
                departureTimeOfDay: null, ref due);

            if (reminder.TravelMinutes is { } travelMinutes)
            {
                // TravelMinutes and PrepareMinutes are subtracted together, in one AddMinutes
                // call, not as two separate subtractions. TimeOnly wraps at midnight rather than
                // throwing - the wrappedDays out-parameter is what tells us that happened - but it
                // only reports ONE wrap correctly per call; subtracting travel time and then
                // PrepareMinutes as two separate calls can lose track of (or double-count) a
                // day-wrap that only shows up once the two are combined, landing the notification
                // on the wrong date. Combining them first avoids that entirely.
                var minutesBeforeTimeOfDay = travelMinutes + PrepareMinutes;
                var leaveTimeOfDay = timeOfDay.AddMinutes(-minutesBeforeTimeOfDay, out var wrappedDays);
                var leaveDate = reminder.Date.AddDays(wrappedDays);

                // leaveTimeOfDay IS the departure time to show in the notification's own text
                // (DueReminderNotification.DepartureTimeOfDay) - already household-local
                // (Europe/Stockholm) wall-clock time, since that is what Reminder.TimeOfDay
                // itself is (see HouseholdClock.TryToUtc below). No separate UTC-to-local
                // conversion is needed or wanted here.
                TryAdd(
                    reminder, ReminderNotificationKind.TimeToLeave, leaveDate, leaveTimeOfDay, now, window,
                    departureTimeOfDay: leaveTimeOfDay, ref due);
            }
        }

        return due ?? (IReadOnlyList<DueReminderNotification>)[];
    }

    private static void TryAdd(
        Reminder reminder,
        ReminderNotificationKind kind,
        DateOnly date,
        TimeOnly timeOfDay,
        DateTimeOffset now,
        TimeSpan window,
        TimeOnly? departureTimeOfDay,
        ref List<DueReminderNotification>? due)
    {
        // A wall-clock moment that never happened (the spring DST gap) has no UTC instant to
        // compare - there is nothing due here, not an error.
        if (!HouseholdClock.TryToUtc(date, timeOfDay, out var instant))
        {
            return;
        }

        // A reminder's time of day is minute-precise by nature (docs/PRODUCT.md §11: it comes
        // from an <input type="time">, never seconds), but ISentReminderNotificationRepository
        // compares this value for exact DateTimeOffset equality against what it reads back from
        // Postgres, whose timestamptz only keeps microsecond precision against .NET's 100ns
        // ticks. Truncating to the minute here means that comparison never has to rely on nobody
        // ever adding sub-minute precision to a reminder's time - the stored and recomputed
        // values are identical by construction, not by accident.
        var scheduledFor = instant.AddTicks(-(instant.Ticks % TimeSpan.TicksPerMinute));

        if (scheduledFor > now || scheduledFor <= now - window)
        {
            return;
        }

        (due ??= []).Add(new DueReminderNotification(
            reminder.Id, reminder.HouseholdId, reminder.MemberId, kind, scheduledFor, reminder.Title,
            reminder.Location, departureTimeOfDay));
    }
}
