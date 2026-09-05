using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Replaces a member's normal weekly time budget - how much time they have on an ordinary
/// week, as opposed to a single day's override. See <see cref="SetMemberAvailability"/> for that.
/// </summary>
public sealed class SetMemberWeeklyBudget
{
    private readonly IHouseholdRepository _households;

    public SetMemberWeeklyBudget(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Sets the budget, or returns <c>null</c> when the household has no such member.
    /// </summary>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId,
        Guid memberId,
        WeeklyTimeBudget weeklyTimeBudget,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (household is null || member is null)
        {
            return null;
        }

        member.ChangeWeeklyTimeBudget(weeklyTimeBudget);
        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
