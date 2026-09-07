using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Refreshes members whose <see cref="HouseholdMember.WeeklyTimeBudget"/> still matches an OLD
/// role-preset formula to the CURRENT one for their <see cref="HouseholdMember.Role"/> - a
/// one-time, explicit, idempotent migration for households created before a preset's numbers
/// last changed (see docs/ARCHITECTURE.md, "65/35 target split").
/// </summary>
/// <remarks>
/// Never touches a member whose stored budget does not match the OLD formula exactly - either
/// it was already refreshed (a second run is a no-op, by construction), or the household edited
/// it by hand, and a real customization must never be silently overwritten by a general
/// migration. Nothing here runs automatically; it is only ever invoked explicitly (see
/// <c>PUT .../members/refresh-role-budgets</c>), never at application startup.
/// <para>
/// The OLD/NEW formulas below are a deliberate, documented duplication of
/// <c>Hemordna.Client.Support.HouseholdRolePresets.BudgetFor</c> - a Client-only type, since the
/// client intentionally keeps its own copy of every wire shape rather than referencing the
/// server assemblies (see <c>Hemordna.Client.Contracts.ApiContracts</c>'s file header), so this
/// migration cannot reference it directly. Only <see cref="HouseholdRole.AdultFullTime"/> and
/// <see cref="HouseholdRole.Retired"/> changed (to a 7:13 / 35%:65% ratio) -
/// <see cref="HouseholdRole.ChildOrTeen"/> is untouched by the 65/35 decision and therefore not
/// migrated here.
/// </para>
/// </remarks>
public sealed class RefreshRolePresetBudgets
{
    private static readonly WeeklyTimeBudget OldAdultFullTime = WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int>
    {
        [DayOfWeek.Monday] = 30,
        [DayOfWeek.Tuesday] = 30,
        [DayOfWeek.Wednesday] = 30,
        [DayOfWeek.Thursday] = 30,
        [DayOfWeek.Friday] = 30,
        [DayOfWeek.Saturday] = 60,
        [DayOfWeek.Sunday] = 60
    });

    private static readonly WeeklyTimeBudget NewAdultFullTime = WeeklyTimeBudget.Uniform(35);
    private static readonly WeeklyTimeBudget OldRetired = WeeklyTimeBudget.Uniform(60);
    private static readonly WeeklyTimeBudget NewRetired = WeeklyTimeBudget.Uniform(65);

    private readonly IHouseholdRepository _households;

    public RefreshRolePresetBudgets(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Refreshes this household's members, or returns <c>null</c> when it does not exist.
    /// Returns how many members were actually changed.
    /// </summary>
    public async Task<int?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        var changed = 0;

        foreach (var member in household.Members)
        {
            if (member.Role == HouseholdRole.AdultFullTime && member.WeeklyTimeBudget.Equals(OldAdultFullTime))
            {
                member.ChangeWeeklyTimeBudget(NewAdultFullTime);
                changed++;
            }
            else if (member.Role == HouseholdRole.Retired && member.WeeklyTimeBudget.Equals(OldRetired))
            {
                member.ChangeWeeklyTimeBudget(NewRetired);
                changed++;
            }
        }

        if (changed > 0)
        {
            await _households.UpdateAsync(household, cancellationToken);
        }

        return changed;
    }
}
