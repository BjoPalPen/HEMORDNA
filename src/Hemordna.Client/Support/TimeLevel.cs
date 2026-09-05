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
        ("Lite tid", 15),
        ("Lagom tid", 30),
        ("Gott om tid", 60)
    ];

    /// <summary>The closest level to a stored minute value, for pre-selecting an editor.</summary>
    public static int ClosestMinutes(int minutes) => All.MinBy(level => Math.Abs(level.Minutes - minutes)).Minutes;
}
