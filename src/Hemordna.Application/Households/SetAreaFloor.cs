using Hemordna.Domain.Areas;

namespace Hemordna.Application.Households;

/// <summary>Moves an existing area to a (possibly different) floor, or clears its floor.</summary>
public sealed class SetAreaFloor
{
    private readonly IHouseholdRepository _households;

    public SetAreaFloor(IHouseholdRepository households) => _households = households;

    /// <summary>Moves the area, or returns <c>null</c> when the household has no such area.</summary>
    public async Task<Area?> HandleAsync(
        Guid householdId, Guid areaId, string? floor, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null || household.Areas.All(a => a.Id != areaId))
        {
            return null;
        }

        var area = household.SetAreaFloor(areaId, floor);
        await _households.UpdateAsync(household, cancellationToken);

        return area;
    }
}
