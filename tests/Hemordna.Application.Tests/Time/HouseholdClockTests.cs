using Hemordna.Application.Tests.Households;
using Hemordna.Application.Time;

namespace Hemordna.Application.Tests.Time;

/// <summary>
/// Covers the one behavior that matters here: the server's "today" must match Stockholm wall
/// time, not UTC. The failure mode this guards against only shows up in the roughly two-hour
/// (summer) or one-hour (winter) window right after UTC midnight, where the UTC calendar date
/// is still "yesterday" but Stockholm has already turned over to a new day.
/// </summary>
public class HouseholdClockTests
{
    [Fact]
    public void Today_DuringSummerMidnightWindow_ReturnsStockholmDate()
    {
        // 22:30 UTC on July 1st is 00:30 in Stockholm (UTC+2 under CEST) on July 2nd - a plain
        // UTC-date read would still say July 1st.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 22, 30, 0, TimeSpan.Zero));

        var today = HouseholdClock.Today(clock);

        Assert.Equal(new DateOnly(2026, 7, 2), today);
    }

    [Fact]
    public void Today_DuringWinterMidnightWindow_ReturnsStockholmDate()
    {
        // 23:30 UTC on January 15th is 00:30 in Stockholm (UTC+1 under CET) on January 16th.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero));

        var today = HouseholdClock.Today(clock);

        Assert.Equal(new DateOnly(2026, 1, 16), today);
    }

    [Fact]
    public void Today_AtMidday_MatchesBothZones()
    {
        // Midday UTC falls on the same calendar date in Stockholm regardless of season, so this
        // case fails only if the conversion is broken in some other way than the offset window.
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));

        var today = HouseholdClock.Today(clock);

        Assert.Equal(new DateOnly(2026, 7, 1), today);
    }
}
