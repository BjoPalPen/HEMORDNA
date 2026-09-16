using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Replaces a member's weekly effort ceiling - the heaviest task they take on, per weekday. See
/// <see cref="WeeklyEffortCeiling"/> and <see cref="SetMemberWeeklyBudget"/> for the equivalent
/// time-budget use case this mirrors.
/// </summary>
public sealed class SetMemberWeeklyEffortCeiling
{
    private readonly IHouseholdRepository _households;

    public SetMemberWeeklyEffortCeiling(IHouseholdRepository households) => _households = households;

    /// <summary>Sets the ceiling, or returns <c>null</c> when the household has no such member.</summary>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId,
        Guid memberId,
        WeeklyEffortCeiling weeklyEffortCeiling,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (household is null || member is null)
        {
            return null;
        }

        member.ChangeWeeklyEffortCeiling(weeklyEffortCeiling);
        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
