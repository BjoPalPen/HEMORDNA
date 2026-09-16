namespace Hemordna.Domain.Tasks;

/// <summary>
/// How much a task takes out of the person doing it, independent of how long it takes - a short
/// task can still be heavy (scrubbing an oven), and a long one can be light (folding laundry
/// while watching TV). Deliberately three qualitative levels, never a number or a score - see
/// docs/PRODUCT.md §8: no point tallies, no comparisons. UI labels are "Lätt", "Mellan", "Tung".
/// </summary>
public enum TaskEffort
{
    Light,
    Medium,
    Heavy
}
