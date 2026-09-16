namespace Hemordna.Client.Support;

/// <summary>
/// How much a task takes out of whoever does it - independent of how long it takes. A
/// client-side mirror of the domain's own <c>TaskEffort</c>, same pattern as
/// <see cref="HouseholdRole"/>: the client keeps its own small copy rather than referencing the
/// server's assemblies (see ApiContracts.cs), and enum-shaped fields travel over the wire as
/// plain strings.
/// </summary>
public enum TaskEffort
{
    Light,
    Medium,
    Heavy
}

/// <summary>Always shown as one of three words, never a number - see docs/PRODUCT.md §8.</summary>
public static class EffortLevel
{
    public static readonly (TaskEffort Effort, string Label)[] All =
    [
        (TaskEffort.Light, "Lätt"),
        (TaskEffort.Medium, "Mellan"),
        (TaskEffort.Heavy, "Tung")
    ];

    public static string LabelFor(TaskEffort effort) => All.First(level => level.Effort == effort).Label;

    public static string LabelFor(string effort)
        => Enum.TryParse<TaskEffort>(effort, out var parsed) ? LabelFor(parsed) : effort;
}
