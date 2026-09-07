namespace Hemordna.Application.Planning;

/// <summary>
/// A soft nudge for a handful of well-known "do X before Y" chore pairs (currently just:
/// vacuum or sweep a floor before mopping it - dirt goes down first). Applied by
/// <see cref="DailyPlanner"/> only as the very last tiebreak, after every planning-relevant
/// criterion (deferrable, overdue, priority, due date, estimated minutes) - it can never change
/// WHAT is planned versus deferred, only the display order of two tasks that are otherwise
/// exactly tied and happen to land on the same day.
/// </summary>
/// <remarks>
/// Deliberately narrow keyword matching rather than a general "chore ordering" system - the
/// concrete, requested case is floor care, and reaching further (e.g. a blanket "dust before
/// vacuum before wipe before mop" ontology) would guess at preferences nobody asked for. New
/// pairs can be added here the same way if a similar concrete need comes up.
/// </remarks>
internal static class ChoreSequenceHint
{
    public static int RankFor(string taskName)
    {
        var lower = taskName.ToLowerInvariant();

        if (lower.Contains("torka golvet") || lower.Contains("moppa"))
        {
            return 1;
        }

        if (lower.Contains("dammsug") || lower.Contains("sopa golvet"))
        {
            return -1;
        }

        return 0;
    }
}
