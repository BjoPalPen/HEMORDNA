namespace Hemordna.Client.Support;

/// <summary>
/// Time is a planning input, not something a person should have to think in minutes about -
/// see PRODUCT.md §4/§8. Every place that used to ask for or show a minute count now offers
/// this small, qualitative scale instead; the real integer still goes to the API underneath.
/// </summary>
public static class TimeLevel
{
    public static readonly (string Label, int Minutes)[] All =
    [
        ("Ingen tid", 0),
        ("Lite tid", 5),
        ("Lagom tid", 15),
        ("Lång tid", 30),
        // Some tasks are not a chore's own effort but an errand around it - "Handla mat" can
        // mean a long drive somewhere, not just filling a basket - see docs/ARCHITECTURE.md
        // "Beslut: Fler tidsnivåer, upp till flera timmar".
        ("En timme", 60),
        ("Flera timmar", 120)
    ];

    /// <summary>The closest level to a stored minute value, for pre-selecting an editor.</summary>
    public static int ClosestMinutes(int minutes) => All.MinBy(level => Math.Abs(level.Minutes - minutes)).Minutes;

    /// <summary>The closest level's label for a task that DOES take some time - "0 minuter, no
    /// chip at all" is the caller's own gate (Item.EstimatedMinutes > 0), so this only ever
    /// chooses among the non-zero levels, never "Ingen tid". Still used for the level-picker
    /// buttons themselves (choosing IS what the level words are for - see
    /// <see cref="MinutesLabel"/>'s own remarks) - never for a row that just shows a saved time.</summary>
    public static string LabelFor(int minutes)
        => All.Where(level => level.Minutes > 0).MinBy(level => Math.Abs(level.Minutes - minutes)).Label;

    /// <summary>
    /// The exact stored minute count, formatted for display - "5 min", never rounded to the
    /// nearest level and never a level word. Level words are for CHOOSING among a handful of
    /// options; once a value is picked and shown back (a task row's own chip, the focus card), the real
    /// number is what was actually saved - "Lite tid" for both a 5- and a 12-minute task reads
    /// as vague information, even though it is a fine choice to pick between. Returns an empty
    /// string for 0, so a caller can omit the row entirely rather than render "0 min" - see
    /// docs/DESIGN.md §6a.
    /// </summary>
    public static string MinutesLabel(int minutes) => minutes > 0 ? $"{minutes} min" : string.Empty;
}
