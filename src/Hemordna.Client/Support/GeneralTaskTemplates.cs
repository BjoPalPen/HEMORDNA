namespace Hemordna.Client.Support;

/// <summary>
/// The room-free counterpart to <see cref="RoomTemplates"/>: common household chores that do
/// not belong to any particular room ("Handla mat", "Rasta hunden"), offered as suggestions on
/// Områden's "Övrigt" card instead of only ever being typed in by hand one at a time.
/// </summary>
/// <remarks>
/// Unlike a room's checklist - where nearly every listed task genuinely applies once you have
/// named the room's type - whether these apply varies a lot per household (not everyone has a
/// pet, not everyone pays bills the same way), so they start unchecked rather than selected by
/// default. Reuses <see cref="RoomTemplateTask"/>'s shape (name, minutes, frequency) since the
/// scheduling need is identical.
/// </remarks>
public static class GeneralTaskTemplates
{
    public static readonly IReadOnlyList<RoomTemplateTask> All =
    [
        new("Handla mat", 45, TaskFrequency.Weekly),
        new("Tvätta och lägga in tvätt", 20, TaskFrequency.Weekly),
        new("Betala räkningar", 10, TaskFrequency.Monthly),
        new("Sortera och lämna återvinning", 10, TaskFrequency.Weekly),
        new("Vattna växter", 5, TaskFrequency.Weekly),
        new("Rasta hunden", 20, TaskFrequency.Daily),
        new("Byta kattlåda", 5, TaskFrequency.Weekly),
        new("Rensa kylskåpet", 10, TaskFrequency.AsNeeded)
    ];
}
