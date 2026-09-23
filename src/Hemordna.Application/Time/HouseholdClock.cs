namespace Hemordna.Application.Time;

/// <summary>
/// The single source of truth for "what day is it" on the server. Every server-derived date
/// decision (a fallback "today" when a client does not send one, a rebalance anchor, a
/// newly-created recurrence's start date, and so on) must go through <see cref="Today"/>
/// instead of reading UTC directly.
/// </summary>
/// <remarks>
/// The zone is deliberately hard-coded to Stockholm rather than using
/// <see cref="TimeProvider.GetLocalNow(TimeProvider)"/>-style local time. The production
/// container runs with its OS clock set to UTC and no <c>TZ</c> environment variable, so
/// "local time" there already IS UTC - <c>GetLocalNow()</c> would silently keep the exact bug
/// this type exists to fix. Naming the zone explicitly means the server's notion of "today"
/// depends on the household's real-world timezone, not on how the container happens to be
/// configured. Do not "simplify" this back to local time or to <c>DateTime.UtcNow</c> - both
/// were tried, and both put the server a day behind the client for roughly the first one to
/// two hours after UTC midnight, which is exactly the bug this class fixes.
/// </remarks>
public static class HouseholdClock
{
    private const string ZoneId = "Europe/Stockholm";

    private static readonly TimeZoneInfo Zone = ResolveZone();

    /// <summary>
    /// <c>true</c> when <see cref="ZoneId"/> could not be resolved on this machine and UTC is
    /// being used as a fallback instead. A static class cannot inject a logger, so this flag is
    /// the hook a later, logger-capable layer (the Api layer) reads to report the fallback -
    /// it is not acted on here.
    /// </summary>
    public static bool UsingFallback { get; private set; }

    /// <summary>
    /// Returns today's date in the household's real-world timezone (Stockholm), derived from
    /// <paramref name="clock"/>'s UTC time. Never reads a wall clock directly - see CLAUDE.md
    /// §5 - so callers stay deterministic under test.
    /// </summary>
    public static DateOnly Today(TimeProvider clock) => Today(clock.GetUtcNow());

    /// <summary>
    /// Same as <see cref="Today(TimeProvider)"/>, for a caller that already has an instant (for
    /// example a background service that read <see cref="TimeProvider.GetUtcNow"/> once and
    /// wants every downstream calculation - "today", "is this due" - to agree on the exact same
    /// instant rather than each reading the clock separately.
    /// </summary>
    public static DateOnly Today(DateTimeOffset utcNow)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, Zone).DateTime);

    /// <summary>
    /// Converts a household-local wall-clock date and time (Europe/Stockholm) to the UTC instant
    /// it represents - the counterpart to <see cref="Today(TimeProvider)"/>: that answers "what
    /// day is it", this answers "when, in UTC, does this Stockholm wall-clock moment actually
    /// happen". Deliberately goes through this type's own <see cref="Zone"/> rather than
    /// <c>DateTime.Now</c>/<c>GetLocalNow()</c> or a hard-coded UTC offset, so callers (for
    /// example <c>Hemordna.Application.Push.ReminderNotificationSelector</c>, turning a
    /// <c>Reminder.TimeOfDay</c> into a comparable instant) get the correct instant across a
    /// daylight-saving transition instead of a fixed +1 or +2 hours.
    /// </summary>
    /// <returns>
    /// <c>false</c> for the roughly one hour each spring the clocks skip over (02:00-02:59 on
    /// the DST-start Sunday in March does not exist in Stockholm time) - there is no correct UTC
    /// instant to return for a wall-clock moment that was never reached, so the caller is
    /// expected to treat that as "nothing to compute here" rather than fail. The one hour each
    /// autumn that repeats (DST-end Sunday in October) is not ambiguous from this method's point
    /// of view: <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> resolves it
    /// to its standard-time (winter) occurrence, deterministically, every time.
    /// </returns>
    public static bool TryToUtc(DateOnly date, TimeOnly timeOfDay, out DateTimeOffset utc)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(timeOfDay), DateTimeKind.Unspecified);

        if (Zone.IsInvalidTime(local))
        {
            utc = default;
            return false;
        }

        utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Zone), TimeSpan.Zero);
        return true;
    }

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Deliberate: never throw from here. A missing timezone database must not take the
            // whole server down over a date calculation - see the remarks on this type, and
            // UsingFallback above for how this gets surfaced instead.
            UsingFallback = true;
            return TimeZoneInfo.Utc;
        }
    }
}
