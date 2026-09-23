namespace Hemordna.E2E.Tests;

/// <summary>
/// The date the app under test considers "today".
/// </summary>
/// <remarks>
/// Must be LOCAL, not UTC. The client derives its own day from
/// <c>TimeProvider.GetLocalNow()</c> (see <c>MinDag.razor</c>'s <c>Today</c>), so a test that
/// schedules work for the UTC date puts it on a DIFFERENT day than the page renders whenever the
/// two dates disagree - in Sweden that is every night between local midnight and 02:00 (CEST) or
/// 01:00 (CET). The occurrence then shows up as "sedan tidigare" instead of as today's work, and
/// the test fails for a reason that has nothing to do with what it is testing.
/// <para>
/// This bit the suite for real: <c>ExtraTaskTests.Extra_uppgift_offers_a_pick_list_grouped_by_room</c>
/// failed a run that crossed local midnight, and failed identically on an untouched <c>main</c>.
/// Before this type existed, 60 places used the UTC date and 8 had already been patched to the
/// local one by hand - one definition removes the choice.
/// </para>
/// </remarks>
internal static class AppDate
{
    /// <summary>Today, in the same time zone the browser and the page use.</summary>
    internal static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
