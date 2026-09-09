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
}
