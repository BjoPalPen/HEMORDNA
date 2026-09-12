using Hemordna.Application.Tasks;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Wipes a household back to its just-created state: every area, every task definition - and
/// with it, by database cascade, every occurrence and assignment - and every time-credit
/// ledger row are gone. The household itself, its invite code, and every member stay, so
/// signing back in lands the household on the same empty-room screen a brand new household
/// sees - see docs/ARCHITECTURE.md "Beslut: Rensa ett hushåll".
/// </summary>
/// <remarks>
/// A hard delete, not a deactivation. Every other removal in the app (<see cref="DeactivateArea"/>,
/// <see cref="DeactivateHouseholdMember"/>, <see cref="Hemordna.Application.Tasks.DeactivateTaskDefinition"/>)
/// keeps history pointing at a real entity; here there is no history left to protect, because
/// everything is being discarded in the same operation.
/// </remarks>
public sealed class ResetHousehold
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly IMemberTimeCreditRepository _timeCredits;

    public ResetHousehold(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        IMemberTimeCreditRepository timeCredits)
    {
        _households = households;
        _definitions = definitions;
        _timeCredits = timeCredits;
    }

    /// <summary>Resets the household, or returns <c>null</c> when it does not exist.</summary>
    public async Task<Household?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        // Task definitions first: their own AreaId otherwise only gets set to null by the
        // database when an area is removed while a definition still references it. Deleting
        // definitions first means there is nothing left to null out by the time areas go.
        await _definitions.DeleteAllByHouseholdAsync(householdId, cancellationToken);
        await _timeCredits.DeleteAllByHouseholdAsync(householdId, cancellationToken);

        household.ClearAreas();
        await _households.UpdateAsync(household, cancellationToken);

        return household;
    }
}
