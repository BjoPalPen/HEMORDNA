using Hemordna.Domain.Common;

namespace Hemordna.Domain.Areas;

/// <summary>
/// A part of the home that work belongs to - a room ("Kok", "Badrum") or any other grouping
/// the household chooses ("Tradgard", "Hund"). Households define their own areas.
/// </summary>
public sealed class Area
{
    private Area(Guid id, Guid householdId, string name, string? floor)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        Floor = floor;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// The grouping this area belongs to, e.g. "Våning 1" - a household's own free-text label,
    /// not a fixed set of storeys. <c>null</c> means no grouping. Independent of <see cref="Name"/>:
    /// renaming an area never touches its floor, and moving an area to a different floor (see
    /// <see cref="SetFloor"/>) never touches its name.
    /// </summary>
    public string? Floor { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// The last day this room's own schedule is paused, inclusive - e.g. a renovation.
    /// <c>null</c> means not paused. Unlike a household or member pause, which only ever
    /// affects what gets generated next, pausing a room also clears whatever is already
    /// outstanding for it - see <c>PauseArea</c>, in <c>Hemordna.Application</c>.
    /// </summary>
    public DateOnly? PausedUntil { get; private set; }

    internal static Area Create(Guid householdId, string name, string? floor = null)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));

        return new Area(
            Guid.NewGuid(),
            householdId,
            Guard.AgainstNullOrWhiteSpace(name, nameof(name)),
            NormalizeFloor(floor));
    }

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    /// <summary>Moves this area to a (possibly different) floor, or clears its floor when <paramref name="floor"/> is blank.</summary>
    public void SetFloor(string? floor) => Floor = NormalizeFloor(floor);

    private static string? NormalizeFloor(string? floor)
        => string.IsNullOrWhiteSpace(floor) ? null : floor.Trim();

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Pauses this room's schedule through and including <paramref name="until"/>.</summary>
    public void Pause(DateOnly until) => PausedUntil = until;

    public void Resume() => PausedUntil = null;

    public bool IsPausedOn(DateOnly date) => PausedUntil is { } until && date <= until;
}
