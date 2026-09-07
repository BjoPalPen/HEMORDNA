using Hemordna.Domain.Areas;

namespace Hemordna.Application.Households;

/// <summary>Renames an existing room/area - previously only choosable at creation.</summary>
public sealed class RenameArea
{
    private readonly IHouseholdRepository _households;

    public RenameArea(IHouseholdRepository households) => _households = households;

    /// <summary>Renames the area, or returns <c>null</c> when the household has no such area.</summary>
    public async Task<Area?> HandleAsync(
        Guid householdId, Guid areaId, string name, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var area = household?.Areas.FirstOrDefault(a => a.Id == areaId);

        if (household is null || area is null)
        {
            return null;
        }

        area.Rename(name);
        await _households.UpdateAsync(household, cancellationToken);

        return area;
    }
}
