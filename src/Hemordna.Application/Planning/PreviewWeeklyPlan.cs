using Hemordna.Application.Households;
using Hemordna.Application.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// "Planera veckan" - a read-only preview of how the household's weekly and monthly tasks would
/// spread across the week if placed fresh, given current capacity and effort ceilings. Saves
/// nothing; see <see cref="ApplyWeeklyPlan"/> for the use case that writes the result.
/// </summary>
public sealed class PreviewWeeklyPlan
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly WeeklyPlacementPlanner _planner = new();

    public PreviewWeeklyPlan(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    /// <summary>Returns the plan, or <c>null</c> when the household does not exist.</summary>
    public async Task<WeeklyPlacementResult?> HandleAsync(Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);
        var request = WeeklyPlacementBuilder.Build(household, definitions, today);

        return _planner.Plan(request);
    }
}
