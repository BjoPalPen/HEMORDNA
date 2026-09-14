using Hemordna.Application.Tasks;
using Hemordna.Domain.Areas;

namespace Hemordna.Application.Households;

/// <summary>
/// Removing a room is a deactivation, not a delete, so history keeps referring to a real area -
/// see Area. Also deactivates the area's own tasks: leaving them active would keep nagging the
/// household about a room that no longer exists, e.g. after a mistake at setup or a move.
/// </summary>
/// <remarks>
/// Deactivating the tasks is still not enough: occurrences already generated from them keep
/// sitting on someone's day, naming a room the household has removed. A real household hit
/// exactly that - two bedrooms replaced by floor-named ones kept 32 planned occurrences alive,
/// showing "Sovrum 1"/"Sovrum 2" among today's work for weeks. They are skipped here, the same
/// thing <see cref="PauseArea"/> does for a paused room, except without an end date because a
/// removal has no "until". Completed occurrences are never touched.
/// </remarks>
public sealed class DeactivateArea
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;

    public DeactivateArea(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
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
        var definitionIds = tasks.Select(task => task.Id).ToHashSet();

        foreach (var task in tasks)
        {
            task.Deactivate();
            await _definitions.UpdateAsync(task, cancellationToken);
        }

        if (definitionIds.Count == 0)
        {
            return area;
        }

        var outstanding = await _occurrences.ListOutstandingByHouseholdAsync(householdId, cancellationToken);

        foreach (var occurrence in outstanding.Where(o => definitionIds.Contains(o.TaskDefinitionId)))
        {
            occurrence.Skip();
            await _occurrences.UpdateAsync(occurrence, cancellationToken);
        }

        return area;
    }
}
