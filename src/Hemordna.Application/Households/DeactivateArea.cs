using Hemordna.Application.Tasks;
using Hemordna.Domain.Areas;

namespace Hemordna.Application.Households;

/// <summary>
/// Removing a room is a deactivation, not a delete, so history keeps referring to a real area -
/// see Area. Also deactivates the area's own tasks: leaving them active would keep nagging the
/// household about a room that no longer exists, e.g. after a mistake at setup or a move.
/// </summary>
public sealed class DeactivateArea
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;

    public DeactivateArea(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    /// <summary>Deactivates the area, or returns <c>null</c> when the household has no such area.</summary>
    public async Task<Area?> HandleAsync(Guid householdId, Guid areaId, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var area = household?.Areas.FirstOrDefault(a => a.Id == areaId);

        if (household is null || area is null)
        {
            return null;
        }

        area.Deactivate();
        await _households.UpdateAsync(household, cancellationToken);

        var tasks = await _definitions.ListActiveByAreaAsync(householdId, areaId, cancellationToken);

        foreach (var task in tasks)
        {
            task.Deactivate();
            await _definitions.UpdateAsync(task, cancellationToken);
        }

        return area;
    }
}
