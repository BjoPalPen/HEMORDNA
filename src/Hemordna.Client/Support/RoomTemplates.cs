namespace Hemordna.Client.Support;

/// <summary>One item a room template generates. The minutes travel to the API but are never shown - see TimeLevel.</summary>
public sealed record RoomTemplateTask(string Name, int EstimatedMinutes);

public sealed record RoomTemplate(string Key, string Label, IReadOnlyList<RoomTemplateTask> Tasks)
{
    /// <summary>What the room's whole checklist should take - shown during setup planning, never during daily use.</summary>
    public int TotalMinutes => Tasks.Sum(task => task.EstimatedMinutes);
}

/// <summary>
/// Naming a room's type gives a ready-made checklist for keeping it tidy, instead of everyone
/// having to think up and enter each task by hand - the second half of the "too many choices to
/// get started" feedback (the first half was time; see HouseholdRolePresets). Every generated
/// task repeats weekly and rotates between household members, the same pattern already used for
/// the seeded "Dammsug vardagsrum" task.
/// </summary>
public static class RoomTemplates
{
    public static readonly IReadOnlyList<RoomTemplate> All =
    [
        new("SmallToilet", "Litet wc",
        [
            new("Torka av handfatet", 5),
            new("Rengör toalettstolen", 10),
            new("Putsa spegeln", 5),
            new("Damma hyllor", 5),
            new("Dammsug golvet", 5),
            new("Torka golvet", 5)
        ]),
        new("Bathroom", "Badrum",
        [
            new("Torka av handfatet", 5),
            new("Rengör toalettstolen", 10),
            new("Skrubba dusch eller badkar", 15),
            new("Putsa spegeln", 5),
            new("Damma hyllor", 5),
            new("Byt handdukar", 5),
            new("Dammsug golvet", 5),
            new("Torka golvet", 10)
        ]),
        new("Kitchen", "Kök",
        [
            new("Diska eller töm diskmaskinen", 15),
            new("Torka av bänkarna", 5),
            new("Rengör spisen", 10),
            new("Töm soptunnan", 5),
            new("Dammsug golvet", 10),
            new("Torka golvet", 10)
        ]),
        new("Bedroom", "Sovrum",
        [
            new("Bädda sängen", 5),
            new("Dammsug golvet", 10),
            new("Damma ytor", 5),
            new("Vädra rummet", 5),
            new("Plocka undan kläder", 10)
        ]),
        new("LivingRoom", "Vardagsrum",
        [
            new("Dammsug golvet", 15),
            new("Damma ytor", 10),
            new("Plocka undan", 10),
            new("Vädra rummet", 5)
        ]),
        new("Hallway", "Hall",
        [
            new("Dammsug eller sopa golvet", 5),
            new("Torka golvet", 5),
            new("Ställ i ordning skorna", 5),
            new("Släng gammal post och reklam", 5)
        ])
    ];
}
