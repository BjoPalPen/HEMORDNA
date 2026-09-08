namespace Hemordna.Application.Planning;

/// <summary>
/// The key two candidates must share to count as "the same cluster" for DailyPlanner's room-
/// affinity tie-break (product feedback: people naturally clean a room, or a floor, in one go
/// rather than hopping between rooms). Either the exact same room, or - when the room's name
/// follows the "Våning – " floor convention the client's own Support.RoomFloors already reads -
/// the same floor, so two different rooms on one floor still cluster together. A candidate with
/// no area at all has no cluster key and never counts as a match for anything, including
/// another area-less candidate - "Övrigt" tasks are not "the same room" as each other just for
/// both having none.
/// </summary>
internal static class TaskCluster
{
    public static string? KeyFor(string? areaName)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return null;
        }

        var separatorIndex = areaName.IndexOf(" – ", StringComparison.Ordinal);
        return separatorIndex > 0 ? areaName[..separatorIndex] : areaName;
    }
}
