namespace Hemordna.Client.Support;

/// <summary>
/// "Hur är orken idag?" (Sju enkla lösningar, del 1) - a one-day-only multiplier on top of the
/// member's own normal per-weekday budget (PRODUCT.md §5's "mindre tid idag utan att den normala
/// veckobudgeten förstörs", given a fixed three-way shape here rather than a free-form number).
/// The three multipliers are starting values, not measured - expect these to move once real
/// usage says otherwise.
/// </summary>
public static class EnergyLevel
{
    public static readonly (string Label, double Multiplier)[] All =
    [
        ("Lite", 0.4),
        ("Lagom", 1.0),
        ("Mycket", 1.3)
    ];

    /// <summary>The day's normal minutes scaled by a level's multiplier, rounded to the nearest
    /// whole minute. Note: rounding to the nearest 5 instead would move the uppdrag's own worked
    /// examples away from their own stated numbers - 60 × 0.4 = 24 and 60 × 1.3 = 78 are already
    /// whole minutes, and neither is a multiple of 5 (nearest-5 would give 25 and 80). The
    /// concrete, testable numbers in the task's own example are treated as authoritative over
    /// the looser "avrundat till närmaste 5" prose - see the report for this part.</summary>
    public static int MinutesFor(int normalMinutes, double multiplier)
        => (int)Math.Round(normalMinutes * multiplier);

    /// <summary>
    /// "Orkvalet styr dagens tyngd" - only "Lite" sets a ceiling on today's tasks ("Lite" →
    /// bara lätta uppgifter idag); "Lagom" and "Mycket" leave it <c>null</c> ("inget
    /// tyngdfilter"), which also clears any ceiling an earlier "Lite" the same day set. Only
    /// "Lite" filters: the person's usual capacity per weekday already gates heavy work at
    /// ASSIGNMENT time (<c>RotationPicker</c>) - filtering it again here, for Lagom/Mycket too,
    /// would strand whatever the rotation fell back to them for, since it would never be shown
    /// and so never get done. See docs/ARCHITECTURE.md "Beslut: orkvalet styr dagens tyngd".
    /// The string travels over the wire as the server's own <c>TaskEffort</c> name - see
    /// ApiContracts.cs's header for why enum-shaped fields are plain strings here.
    /// </summary>
    public static string? EffortCeilingFor(string label) => label == "Lite" ? "Light" : null;
}
