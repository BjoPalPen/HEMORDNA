using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// Even four qualitative levels per weekday was still too many choices at once (product
/// feedback). Most people's week follows the shape of their daily routine, so one role pick
/// infers a whole week's budget instead - see DESIGN.md §6a. Anyone whose week does not fit a
/// role still has the day-by-day editor available behind "Fler alternativ"/"Anpassa varje dag".
/// </summary>
public enum HouseholdRole
{
    AdultFullTime,
    ChildOrTeen,
    Retired
}

public static class HouseholdRolePresets
{
    public static readonly (HouseholdRole Role, string Label)[] All =
    [
        (HouseholdRole.AdultFullTime, "Vuxen, jobbar heltid"),
        (HouseholdRole.ChildOrTeen, "Barn eller ungdom"),
        (HouseholdRole.Retired, "Pensionär / hemma dagtid")
    ];

    /// <summary>
    /// A rough weekly shape per role: less on the days taken up by work or school, more when
    /// free. Not precise - just enough to start from, and always editable afterwards.
    /// </summary>
    public static WeeklyTimeBudgetContract BudgetFor(HouseholdRole role)
    {
        var (weekday, weekend) = role switch
        {
            HouseholdRole.AdultFullTime => (30, 60),
            HouseholdRole.ChildOrTeen => (15, 30),
            HouseholdRole.Retired => (60, 60),
            _ => (30, 30)
        };

        return new WeeklyTimeBudgetContract(weekday, weekday, weekday, weekday, weekday, weekend, weekend);
    }

    /// <summary>The role a stored budget came from, or <c>null</c> when it was set by hand.</summary>
    public static HouseholdRole? Match(WeeklyTimeBudgetContract budget)
    {
        foreach (var (role, _) in All)
        {
            if (BudgetFor(role) == budget)
            {
                return role;
            }
        }

        return null;
    }
}
