namespace Hemordna.Client.Support;

/// <summary>
/// A small icon per activity, guessed from its name (falling back to its area) - what "Bild +
/// text" (see Installningar.razor's PresentationOptions) actually shows. <see cref="For"/>
/// returns a Material Symbols icon name; the matching SVG lives at
/// wwwroot/icons/tasks/{name}.svg - see RoomTasks.razor/MinDag.razor for how it is rendered,
/// and <see cref="DefaultIcon"/> for the guaranteed-to-exist fallback.
/// </summary>
public static class TaskIcons
{
    public const string DefaultIcon = "task_alt";

    // Checked in order, first match in the (lowercased) task name wins.
    private static readonly (string Keyword, string Icon)[] TaskKeywords =
    [
        ("disk", "restaurant"),
        ("bänk", "cleaning_services"),
        ("spis", "kitchen"),
        ("kyl", "kitchen"),
        ("sopt", "delete"),
        ("skräp", "delete"),
        ("släng", "delete"),
        ("golv", "cleaning_services"),
        ("damma", "cleaning_services"),
        ("fönster", "window"),
        ("spegel", "window"),
        ("bädda", "bed"),
        ("säng", "bed"),
        ("vädra", "air"),
        ("kläder", "checkroom"),
        ("handduk", "local_laundry_service"),
        ("tvätt", "local_laundry_service"),
        ("ludd", "local_laundry_service"),
        ("toalett", "wc"),
        ("wc", "wc"),
        ("handfat", "wash"),
        ("dusch", "shower"),
        ("badkar", "bathtub"),
        ("list", "cleaning_services"),
        ("sko", "checkroom"),
        ("post", "mail"),
        ("skrivbord", "desk"),
        ("bord", "table_restaurant"),
        ("hund", "pets"),
        ("katt", "pets"),
        ("gräsmatta", "yard"),
        ("växt", "yard"),
        ("trädgård", "yard"),
        ("bil", "directions_car"),
        ("handla", "shopping_cart"),
        ("mat", "shopping_cart")
    ];

    // Falls back to the room/area when nothing in the task name itself matches.
    private static readonly (string Keyword, string Icon)[] AreaKeywords =
    [
        ("kök", "kitchen"),
        ("badrum", "bathtub"),
        ("wc", "wc"),
        ("toalett", "wc"),
        ("sovrum", "bed"),
        ("vardagsrum", "weekend"),
        ("allrum", "weekend"),
        ("matrum", "table_restaurant"),
        ("hall", "door_front"),
        ("tvättstuga", "local_laundry_service"),
        ("kontor", "desk"),
        ("hund", "pets"),
        ("katt", "pets"),
        ("trädgård", "yard"),
        ("garage", "directions_car"),
        ("bil", "directions_car")
    ];

    public static string For(string taskName, string? areaName)
    {
        var normalizedName = taskName.ToLowerInvariant();

        foreach (var (keyword, icon) in TaskKeywords)
        {
            if (normalizedName.Contains(keyword))
            {
                return icon;
            }
        }

        if (areaName is not null)
        {
            var normalizedArea = areaName.ToLowerInvariant();

            foreach (var (keyword, icon) in AreaKeywords)
            {
                if (normalizedArea.Contains(keyword))
                {
                    return icon;
                }
            }
        }

        return DefaultIcon;
    }
}
