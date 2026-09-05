using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// How often a template task repeats. "AsNeeded" has no calendar slot - it becomes due a fixed
/// number of days after it was last completed instead, see TaskDefinition.StaleAfterDays.
/// </summary>
public enum TaskFrequency
{
    Daily,
    Weekly,
    Monthly,
    AsNeeded
}

/// <summary>One item a room template generates. The minutes travel to the API but are never shown - see TimeLevel.</summary>
public sealed record RoomTemplateTask(string Name, int EstimatedMinutes, TaskFrequency Frequency)
{
    /// <summary>Default "as needed" interval - not shown, and not user-configurable, anywhere it is used.</summary>
    public const int AsNeededDefaultDays = 21;

    /// <summary>The recurrence and/or stale-after-days pair to send when creating this task.</summary>
    public (RecurrenceRuleContract? Recurrence, int? StaleAfterDays) ToScheduling(DateOnly today) => Frequency switch
    {
        TaskFrequency.Daily => (new RecurrenceRuleContract("Daily", 1, today, null, null), null),
        TaskFrequency.Weekly => (new RecurrenceRuleContract("Weekly", 1, today, today.DayOfWeek.ToString(), null), null),
        TaskFrequency.Monthly => (new RecurrenceRuleContract("Monthly", 1, today, null, null), null),
        TaskFrequency.AsNeeded => (null, AsNeededDefaultDays),
        _ => (null, null)
    };
}

public sealed record RoomTemplate(string Key, string Label, IReadOnlyList<RoomTemplateTask> Tasks)
{
    /// <summary>What the room's whole checklist should take - shown during setup planning, never during daily use.</summary>
    public int TotalMinutes => Tasks.Sum(task => task.EstimatedMinutes);
}

/// <summary>
/// Naming a room's type gives a ready-made checklist for keeping it tidy, instead of everyone
/// having to think up and enter each task by hand - the second half of the "too many choices to
/// get started" feedback (the first half was time; see HouseholdRolePresets). Each task's
/// frequency is a sensible default picked here rather than asked of the person setting up the
/// room - see DESIGN.md §6b. Rotating tasks cycle between household members, the same pattern
/// already used for the seeded "Dammsug vardagsrum" task.
/// </summary>
public static class RoomTemplates
{
    public static readonly IReadOnlyList<RoomTemplate> All =
    [
        new("SmallToilet", "Litet wc",
        [
            new("Torka av handfatet", 5, TaskFrequency.Weekly),
            new("Rengör toalettstolen", 10, TaskFrequency.Weekly),
            new("Putsa spegeln", 5, TaskFrequency.AsNeeded),
            new("Damma hyllor", 5, TaskFrequency.AsNeeded),
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Torka golvet", 5, TaskFrequency.Weekly)
        ]),
        new("Bathroom", "Badrum",
        [
            new("Torka av handfatet", 5, TaskFrequency.Weekly),
            new("Rengör toalettstolen", 10, TaskFrequency.Weekly),
            new("Skrubba dusch eller badkar", 15, TaskFrequency.Weekly),
            new("Putsa spegeln", 5, TaskFrequency.AsNeeded),
            new("Damma hyllor", 5, TaskFrequency.AsNeeded),
            new("Byt handdukar", 5, TaskFrequency.Weekly),
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Torka golvet", 10, TaskFrequency.Weekly)
        ]),
        new("Kitchen", "Kök",
        [
            new("Diska eller töm diskmaskinen", 15, TaskFrequency.Daily),
            new("Torka av bänkarna", 5, TaskFrequency.Daily),
            new("Rengör spisen", 10, TaskFrequency.Weekly),
            new("Töm soptunnan", 5, TaskFrequency.Weekly),
            new("Dammsug golvet", 10, TaskFrequency.Weekly),
            new("Torka golvet", 10, TaskFrequency.Weekly)
        ]),
        new("Bedroom", "Sovrum",
        [
            new("Bädda sängen", 5, TaskFrequency.Daily),
            new("Dammsug golvet", 10, TaskFrequency.Weekly),
            new("Damma ytor", 5, TaskFrequency.AsNeeded),
            new("Vädra rummet", 5, TaskFrequency.AsNeeded),
            new("Plocka undan kläder", 10, TaskFrequency.Weekly)
        ]),
        new("LivingRoom", "Vardagsrum",
        [
            new("Dammsug golvet", 15, TaskFrequency.Weekly),
            new("Damma ytor", 10, TaskFrequency.AsNeeded),
            new("Plocka undan", 10, TaskFrequency.Weekly),
            new("Vädra rummet", 5, TaskFrequency.AsNeeded)
        ]),
        new("Hallway", "Hall",
        [
            new("Dammsug eller sopa golvet", 5, TaskFrequency.Weekly),
            new("Torka golvet", 5, TaskFrequency.Weekly),
            new("Ställ i ordning skorna", 5, TaskFrequency.AsNeeded),
            new("Släng gammal post och reklam", 5, TaskFrequency.AsNeeded)
        ])
    ];
}
