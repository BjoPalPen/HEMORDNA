using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// The preview, plus which weekdays a household member just moved a visit to that nobody's
/// <see cref="Hemordna.Domain.Households.WeeklyEffortCeiling"/> can actually handle (Björns
/// beslut: the move is allowed anyway - a requirement wins, same as an existing lock - but the
/// client shows a calm notice instead of staying silent about it). Empty when no moves were
/// submitted, or none of them landed on a day short on capacity.
/// </summary>
public sealed record WeeklyPlanPreviewResult(WeeklyPlacementResult Placement, IReadOnlyList<DayOfWeek> EffortWarningDays);

/// <summary>
/// "Planera veckan" - a read-only preview of how the household's weekly and monthly tasks would
/// spread across the week if placed fresh, given current capacity and effort ceilings, optionally
/// with the household member's own pending moves applied first (Björns krav, "Planera veckan går
/// att ändra"). Saves nothing; see <see cref="ApplyWeeklyPlan"/> for the use case that writes the
/// result.
/// </summary>
public sealed class PreviewWeeklyPlan
{
    private static readonly IReadOnlyDictionary<Guid, DayOfWeek> NoMoves = new Dictionary<Guid, DayOfWeek>();

    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly WeeklyPlacementPlanner _planner = new();

    public PreviewWeeklyPlan(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    /// <summary>
    /// Returns the plan, or <c>null</c> when the household does not exist. <paramref name="moves"/>
    /// (visit key -> chosen weekday, see <see cref="PlaceableVisit.VisitKey"/>) is treated exactly
    /// like an existing <see cref="TaskDefinition.PreferredWeekday"/> lock - reuses the same
    /// mechanism (<see cref="PlaceableVisit.LockedWeekday"/>) instead of a second one.
    /// </summary>
    public async Task<WeeklyPlanPreviewResult?> HandleAsync(
        Guid householdId, DateOnly today, IReadOnlyDictionary<Guid, DayOfWeek>? moves, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);
        var request = WeeklyPlacementBuilder.Build(household, definitions, today);
        var effectiveMoves = moves ?? NoMoves;
        var visits = WeeklyPlacementBuilder.ApplyMoves(request.Visits, effectiveMoves);
        var result = _planner.Plan(request with { Visits = visits });

        // Requirement wins (same as an existing lock) - but a moved visit landing on a day whose
        // ceiling can't cover its effort is worth a calm heads-up, not silence.
        var maxEffortByDay = request.Weekdays.ToDictionary(w => w.Day, w => w.MaxEffort);
        var warningDays = result.PlacedVisits
            .Where(placement => effectiveMoves.ContainsKey(placement.Visit.VisitKey)
                && placement.Visit.RequiredEffort > maxEffortByDay[placement.Day])
            .Select(placement => placement.Day)
            .Distinct()
            .ToList();

        return new WeeklyPlanPreviewResult(result, warningDays);
    }
}
