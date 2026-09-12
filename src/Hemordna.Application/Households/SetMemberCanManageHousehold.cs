using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Grants or removes a member's ability to change rooms, tasks and other members - see
/// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad".
/// </summary>
public sealed class SetMemberCanManageHousehold
{
    private readonly IHouseholdRepository _households;

    public SetMemberCanManageHousehold(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Sets the flag, or returns <c>null</c> when the household has no such member.
    /// </summary>
    /// <exception cref="Domain.Common.DomainException">
    /// The member has no account, or this would leave the household with no active member who
    /// can manage it - see <see cref="Household.SetMemberCanManageHousehold"/>.
    /// </exception>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId, Guid memberId, bool canManage, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null || household.Members.All(m => m.Id != memberId))
        {
            return null;
        }

        var member = household.SetMemberCanManageHousehold(memberId, canManage);
        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
