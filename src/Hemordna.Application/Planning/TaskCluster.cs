namespace Hemordna.Application.Planning;

/// <summary>
/// The key two candidates must share to count as "the same cluster" for DailyPlanner's room-
/// affinity tie-break (product feedback: people naturally clean a room, or a floor, in one go
/// rather than hopping between rooms). Either the exact same room, or - when the area has its
/// own <c>Area.Floor</c> set - the same floor, so two different rooms on one floor still cluster
/// together. Read directly off the area, never parsed out of the room's name - see
/// docs/ARCHITECTURE.md "Två olika rum med samma namn på olika våningar" for why a name can no
/// longer be trusted to carry the floor. A candidate with no area at all has no cluster key and
/// never counts as a match for anything, including another area-less candidate - "Övrigt" tasks
/// are not "the same room" as each other just for both having none.
/// </summary>
internal static class TaskCluster
{
    public static string? KeyFor(string? areaName, string? floor)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(floor) ? areaName : floor;
    }
}
