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

    public static int CountDistinct(IEnumerable<AreaResponse> areas)
        => areas.Select(area => FloorOf(area.Name)).Where(floor => floor is not null).Distinct().Count();
}
