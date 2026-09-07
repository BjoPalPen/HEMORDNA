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
    /// A rough weekly shape per role - not precise, just enough to start from, and always
    /// editable afterwards.
    /// </summary>
    /// <remarks>
    /// <b>AdultFullTime and Retired are uniform across all seven days, deliberately in a 7:13
    /// ratio</b> (245 and 455 minutes a week respectively - 35% / 65% of their combined total).
    /// Uniform rather than a "less on weekdays, more on weekends" split: which days are actually
    /// free varies by job (shift work in retail or healthcare rarely means Saturday/Sunday off),
    /// so the role alone should not assume it - see docs/ARCHITECTURE.md for the full reasoning
    /// behind the 65/35 target. The split itself is never encoded as a rule anywhere: it falls
    /// out purely from <see cref="RotationPicker"/> (server-side) weighing rotation by
    /// <c>WeeklyTimeBudget.TotalWeeklyMinutes</c>, which is entirely role-blind - two members
    /// with these two capacities converge on a 65/35 split themselves, without either role's
    /// name ever entering the rotation logic. <c>ChildOrTeen</c> keeps its original
    /// less-on-schooldays shape - unaffected by the 65/35 decision.
    /// </remarks>
    public static WeeklyTimeBudgetContract BudgetFor(HouseholdRole role)
    {
        if (role == HouseholdRole.AdultFullTime)
        {
            return new WeeklyTimeBudgetContract(35, 35, 35, 35, 35, 35, 35);
        }

        if (role == HouseholdRole.Retired)
        {
            return new WeeklyTimeBudgetContract(65, 65, 65, 65, 65, 65, 65);
        }

        var (weekday, weekend) = role == HouseholdRole.ChildOrTeen ? (15, 30) : (30, 30);
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
