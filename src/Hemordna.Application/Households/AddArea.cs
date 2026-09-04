using Hemordna.Domain.Areas;

namespace Hemordna.Application.Households;

/// <summary>Adds an area - a room or any other grouping the household chooses.</summary>
public sealed class AddArea
{
    private readonly IHouseholdRepository _households;

    public AddArea(IHouseholdRepository households) => _households = households;

    /// <summary>Adds the area, or returns <c>null</c> when the household does not exist.</summary>
    public async Task<Area?> HandleAsync(Guid householdId, string name, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var area = household.AddArea(name);

        await _households.UpdateAsync(household, cancellationToken);

        return area;
    }
}
