using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Issues a fresh invite code for a household, so a code shared with the wrong person (or no
/// longer wanted) stops working. See <see cref="Household.RegenerateInviteCode"/>.
/// </summary>
public sealed class RegenerateInviteCode
{
    private readonly IHouseholdRepository _households;

    public RegenerateInviteCode(IHouseholdRepository households) => _households = households;

    /// <summary>Returns the household with its new code, or <c>null</c> if it does not exist.</summary>
    public async Task<Household?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        household.RegenerateInviteCode();
        await _households.UpdateAsync(household, cancellationToken);

        return household;
    }
}
