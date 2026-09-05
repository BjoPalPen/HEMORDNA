namespace Hemordna.Domain.Households;

/// <summary>
/// A member's rough role in the household. Persisted (unlike the client's own presets used to
/// suggest a starting time budget) because rules beyond time budgeting can depend on it - e.g.
/// keeping a task's rotation away from children, see TaskDefinition.RequiresAdult.
/// </summary>
public enum HouseholdRole
{
    AdultFullTime,
    ChildOrTeen,
    Retired
}
