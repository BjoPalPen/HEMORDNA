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

    internal static Area Create(Guid householdId, string name)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));

        return new Area(Guid.NewGuid(), householdId, Guard.AgainstNullOrWhiteSpace(name, nameof(name)));
    }

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
