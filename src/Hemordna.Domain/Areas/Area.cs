using Hemordna.Domain.Common;

namespace Hemordna.Domain.Areas;

/// <summary>
/// A part of the home that work belongs to - a room ("Kok", "Badrum") or any other grouping
/// the household chooses ("Tradgard", "Hund"). Households define their own areas.
/// </summary>
public sealed class Area
{
    private Area(Guid id, Guid householdId, string name)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public string Name { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// The last day this room's own schedule is paused, inclusive - e.g. a renovation.
    /// <c>null</c> means not paused. Unlike a household or member pause, which only ever
    /// affects what gets generated next, pausing a room also clears whatever is already
    /// outstanding for it - see <c>PauseArea</c>, in <c>Hemordna.Application</c>.
    /// </summary>
    public DateOnly? PausedUntil { get; private set; }

    internal static Area Create(Guid householdId, string name)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));

        return new Area(Guid.NewGuid(), householdId, Guard.AgainstNullOrWhiteSpace(name, nameof(name)));
    }

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Pauses this room's schedule through and including <paramref name="until"/>.</summary>
    public void Pause(DateOnly until) => PausedUntil = until;

    public void Resume() => PausedUntil = null;

    public bool IsPausedOn(DateOnly date) => PausedUntil is { } until && date <= until;
}
