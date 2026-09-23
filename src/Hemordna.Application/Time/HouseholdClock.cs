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
    public static DateOnly Today(TimeProvider clock)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

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
