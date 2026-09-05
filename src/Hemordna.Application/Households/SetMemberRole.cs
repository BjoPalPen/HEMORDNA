using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Sets or clears a member's role. Separate from <see cref="SetMemberWeeklyBudget"/> - the
/// caller decides whether to also update the budget to match the role's preset (Hushall's role
/// picker does both), but the two are independent facts about the member.
/// </summary>
public sealed class SetMemberRole
{
    private readonly IHouseholdRepository _households;

    public SetMemberRole(IHouseholdRepository households) => _households = households;

    /// <summary>Sets the role, or returns <c>null</c> when the household has no such member.</summary>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId,
        Guid memberId,
        HouseholdRole? role,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (household is null || member is null)
        {
            return null;
        }

        member.SetRole(role);
        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
