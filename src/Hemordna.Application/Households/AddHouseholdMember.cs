using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Adds a person to an existing household.</summary>
public sealed class AddHouseholdMember
{
    private readonly IHouseholdRepository _households;
    private readonly TimeProvider _timeProvider;

    public AddHouseholdMember(IHouseholdRepository households, TimeProvider timeProvider)
    {
        _households = households;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Adds the member, or returns <c>null</c> when the household does not exist. A duplicate
    /// display name is rejected by the household itself.
    /// </summary>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId,
        string displayName,
        WeeklyTimeBudget weeklyTimeBudget,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var member = household.AddMember(displayName, weeklyTimeBudget, _timeProvider.GetUtcNow());

        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
