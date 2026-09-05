using Hemordna.Domain.Areas;
using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// A household: one to many people who share a home. This is the tenant and security
/// boundary of the model - everything a client can reach is scoped by <see cref="Id"/>.
/// </summary>
/// <remarks>
/// Members and areas are owned by the household because both are small, bounded sets that
/// are almost always needed together. Task definitions and occurrences are deliberately
/// <em>not</em> owned here: they grow without bound and must be queryable per member and
/// per date without loading the whole household.
/// </remarks>
public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];
    private readonly List<Area> _areas = [];

    private Household(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<HouseholdMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<Area> Areas => _areas.AsReadOnly();

    public static Household Create(string name, DateTimeOffset createdAt)
        => new(Guid.NewGuid(), Guard.AgainstNullOrWhiteSpace(name, nameof(name)), createdAt);

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    /// <summary>Adds a member. Display names must be unique within the household.</summary>
    public HouseholdMember AddMember(
        string displayName,
        WeeklyTimeBudget weeklyTimeBudget,
        DateTimeOffset createdAt,
        HouseholdRole? role = null)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));

        if (_members.Any(m => string.Equals(m.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"A member named '{trimmed}' already exists in this household.");
        }

        var member = HouseholdMember.Create(Id, trimmed, weeklyTimeBudget, createdAt, role);
        _members.Add(member);
        return member;
    }

    /// <summary>Adds an area. Names must be unique within the household.</summary>
    public Area AddArea(string name)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        if (_areas.Any(a => string.Equals(a.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"An area named '{trimmed}' already exists in this household.");
        }

        var area = Area.Create(Id, trimmed);
        _areas.Add(area);
        return area;
    }
}
