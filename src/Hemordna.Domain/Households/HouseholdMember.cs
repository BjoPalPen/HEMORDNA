using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// A person in a household. Members are deactivated rather than deleted so that historical
/// occurrences and completions keep referring to a real member.
/// </summary>
public sealed class HouseholdMember
{
    private HouseholdMember(
        Guid id,
        Guid householdId,
        string displayName,
        WeeklyTimeBudget weeklyTimeBudget,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        DisplayName = displayName;
        WeeklyTimeBudget = weeklyTimeBudget;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public string DisplayName { get; private set; }

    /// <summary>Normal available minutes per weekday. Not metadata - the planner reads it.</summary>
    public WeeklyTimeBudget WeeklyTimeBudget { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// The authenticated user this member signs in as, when one is linked. Members added by
    /// someone else - a child, a partner who has not signed up yet - have no user until they do.
    /// </summary>
    public Guid? UserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static HouseholdMember Create(
        Guid householdId,
        string displayName,
        WeeklyTimeBudget weeklyTimeBudget,
        DateTimeOffset createdAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        ArgumentNullException.ThrowIfNull(weeklyTimeBudget);

        return new HouseholdMember(
            Guid.NewGuid(),
            householdId,
            Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName)),
            weeklyTimeBudget,
            createdAt);
    }

    public void Rename(string displayName)
        => DisplayName = Guard.AgainstNullOrWhiteSpace(displayName, nameof(displayName));

    /// <summary>Replaces the normal weekly budget. Use <see cref="MemberAvailability"/> for one-off days.</summary>
    public void ChangeWeeklyTimeBudget(WeeklyTimeBudget weeklyTimeBudget)
    {
        ArgumentNullException.ThrowIfNull(weeklyTimeBudget);
        WeeklyTimeBudget = weeklyTimeBudget;
    }

    /// <summary>
    /// Links this member to an authenticated user. A member can only be linked once - moving
    /// a member to a different user would silently transfer their history.
    /// </summary>
    public void LinkToUser(Guid userId)
    {
        Guard.AgainstEmpty(userId, nameof(userId));

        if (UserId is { } existing && existing != userId)
        {
            throw new DomainException("This member is already linked to a different user.");
        }

        UserId = userId;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Resolves how many minutes this member has on <paramref name="date"/>: the one-off
    /// override for that date if one exists, otherwise the normal weekly budget.
    /// </summary>
    /// <param name="availabilityOverride">
    /// The member's override for <paramref name="date"/>, or <c>null</c> if none was recorded.
    /// </param>
    public int AvailableMinutesOn(DateOnly date, MemberAvailability? availabilityOverride)
    {
        if (availabilityOverride is null)
        {
            return WeeklyTimeBudget.MinutesFor(date.DayOfWeek);
        }

        if (availabilityOverride.MemberId != Id)
        {
            throw new DomainException("The availability override belongs to a different member.");
        }

        if (availabilityOverride.Date != date)
        {
            throw new DomainException("The availability override is for a different date.");
        }

        return availabilityOverride.AvailableMinutes;
    }
}
