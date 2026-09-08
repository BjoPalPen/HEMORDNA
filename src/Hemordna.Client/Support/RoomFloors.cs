using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// "Våning" is a naming convention, not a field on <c>Area</c> - the domain has no Floor
/// concept, and adding one is outside a client-only step (see docs/ARCHITECTURE.md "Ny form",
/// steg 3). Rooms created together (Rum.razor's floor form) share a "Våning – " name prefix;
/// this reads that prefix back out wherever a floor count or grouping is needed. A room renamed
/// by hand to drop the prefix simply falls out of its floor.
/// </summary>
public static class RoomFloors
{
    public static string? FloorOf(string areaName)
    {
        var separatorIndex = areaName.IndexOf(" – ", StringComparison.Ordinal);
        return separatorIndex > 0 ? areaName[..separatorIndex] : null;
    }

    /// <summary>The room's own name with the "Våning – " prefix stripped, if it has one -
    /// pairs with <see cref="FloorOf"/> so a floor heading and the room heading under it never
    /// repeat the same words.</summary>
    public static string RoomNameOf(string areaName)
    {
        var separatorIndex = areaName.IndexOf(" – ", StringComparison.Ordinal);
        return separatorIndex > 0 ? areaName[(separatorIndex + 3)..] : areaName;
    }

    public static int CountDistinct(IEnumerable<AreaResponse> areas)
        => areas.Select(area => FloorOf(area.Name)).Where(floor => floor is not null).Distinct().Count();
}
