using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Sets or clears a member's role, optionally saving the preset budget and effort ceiling
/// in the same persistence operation. A role-only update preserves custom capacity settings.
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
        CancellationToken cancellationToken,
        WeeklyTimeBudget? weeklyTimeBudget = null,
        WeeklyEffortCeiling? weeklyEffortCeiling = null)
    {
        if ((weeklyTimeBudget is null) != (weeklyEffortCeiling is null))
        {
            throw new ArgumentException("A role preset requires both budget and effort ceiling.");
        }

        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (household is null || member is null)
        {
            return null;
        }

        member.SetRole(role);
        if (weeklyTimeBudget is not null && weeklyEffortCeiling is not null)
        {
            member.ChangeWeeklyTimeBudget(weeklyTimeBudget);
            member.ChangeWeeklyEffortCeiling(weeklyEffortCeiling);
        }

        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
