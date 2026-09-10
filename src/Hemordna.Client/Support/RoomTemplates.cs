using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// How often a template task repeats. "AsNeeded" has no calendar slot - it becomes due a fixed
/// number of days after it was last completed instead, see TaskDefinition.StaleAfterDays.
/// "TwiceWeekly"/"EveryOtherDay" have no single weekly slot either - a week's recurrence only
/// carries one weekday (see RecurrenceRule.Weekly) - so both are approximated as a task that
/// comes due every few days rather than on the same fixed weekdays every week; see
/// ToScheduling.
/// </summary>
public enum TaskFrequency
{
    Daily,
    TwiceWeekly,
    Weekly,
    Monthly,
    AsNeeded,
    EveryOtherDay
}

/// <summary>
/// One item a room template generates. The minutes travel to the API but are never shown - see
/// TimeLevel. <paramref name="AdultsOnly"/> keeps the task's rotation away from children (e.g.
/// washing windows) - see TaskDefinition.RequiresAdult. <paramref name="ScalesWithHouseholdSize"/>
/// marks a task whose real-world frequency depends on how many people generate the work (e.g.
/// laundry loads) rather than how dirty a fixed room gets - see FrequencyFor.
/// </summary>
public sealed record RoomTemplateTask(
    string Name, int EstimatedMinutes, TaskFrequency Frequency, bool AdultsOnly = false,
    bool ScalesWithHouseholdSize = false)
{
    /// <summary>Default "as needed" interval - not shown, and not user-configurable, anywhere it is used.</summary>
    public const int AsNeededDefaultDays = 21;

    /// <summary>
    /// Every 3 days averages out to a little over twice a week without pinning it to the same
    /// two weekdays - close enough for a chore reminder, and it drifts across the week over
    /// time instead of always landing on, say, Tuesday and Friday.
    /// </summary>
    private const int TwiceWeeklyIntervalDays = 3;

    /// <summary>
    /// A task per-omgång (e.g. one laundry load) roughly needs personer/1.5 rounds a week -
    /// rounded to whichever calendar cadence is already expressible, rather than a new
    /// recurrence type (PRODUCT.md §9: the recurrence engine is not overdesigned ahead of a
    /// real need). 1 member keeps the template's own default (Weekly); 2-3 doubles it
    /// (TwiceWeekly, i.e. every 3 days); 4+ doubles it again (EveryOtherDay). Never returns a
    /// LOWER frequency than the task's own default - a household that started small and only
    /// grows never sees laundry become rarer than what they already set up.
    /// </summary>
    public TaskFrequency FrequencyFor(int activeMembers)
    {
        if (!ScalesWithHouseholdSize)
        {
            return Frequency;
        }

        return activeMembers switch
        {
            <= 1 => Frequency,
            <= 3 => TaskFrequency.TwiceWeekly,
            _ => TaskFrequency.EveryOtherDay
        };
    }

    /// <summary>
    /// The recurrence and/or stale-after-days pair to send when creating this task.
    /// <paramref name="spreadIndex"/> is this task's position within a batch of tasks created
    /// together (a whole room, or several rooms in one floor) - without it, every weekly task
    /// set up in the same sitting anchors to the SAME weekday (today's), and every monthly one
    /// to the same day-of-month, so a household ends up with one overloaded day and several
    /// empty ones instead of a spread week. Each increase in <paramref name="spreadIndex"/>
    /// moves the anchor to a different weekday (Weekly) or day-of-month/cycle-phase
    /// (Monthly/TwiceWeekly/EveryOtherDay); Daily and AsNeeded have no anchor to spread.
    /// <paramref name="activeMembers"/> only matters for a task with
    /// <see cref="ScalesWithHouseholdSize"/> set - see <see cref="FrequencyFor"/>.
    /// </summary>
    public (RecurrenceRuleContract? Recurrence, int? StaleAfterDays) ToScheduling(
        DateOnly today, int spreadIndex = 0, int activeMembers = 1)
    {
        var shiftedAnchor = today.AddDays(spreadIndex);

        return FrequencyFor(activeMembers) switch
        {
            TaskFrequency.Daily => (new RecurrenceRuleContract("Daily", 1, today, null, null), null),
            TaskFrequency.TwiceWeekly
                => (new RecurrenceRuleContract("Daily", TwiceWeeklyIntervalDays, shiftedAnchor, null, null), null),
            TaskFrequency.EveryOtherDay
                => (new RecurrenceRuleContract("Daily", 2, shiftedAnchor, null, null), null),
            TaskFrequency.Weekly
                => (new RecurrenceRuleContract("Weekly", 1, today, shiftedAnchor.DayOfWeek.ToString(), null), null),
            TaskFrequency.Monthly => (new RecurrenceRuleContract("Monthly", 1, shiftedAnchor, null, null), null),
            TaskFrequency.AsNeeded => (null, AsNeededDefaultDays),
            _ => (null, null)
        };
    }
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
            new("Torka av handfatet", 2, TaskFrequency.Weekly),
            new("Rengör toalettstolen", 5, TaskFrequency.Weekly),
            new("Putsa spegeln", 2, TaskFrequency.AsNeeded),
            new("Damma hyllor", 2, TaskFrequency.AsNeeded),
            new("Dammsug golvet", 3, TaskFrequency.Weekly),
            new("Torka golvet", 3, TaskFrequency.Weekly)
        ]),
        new("Bathroom", "Badrum",
        [
            new("Torka av handfatet", 2, TaskFrequency.Weekly),
            new("Rengör toalettstolen", 5, TaskFrequency.Weekly),
            new("Skrubba dusch eller badkar", 8, TaskFrequency.Weekly),
            new("Putsa spegeln", 2, TaskFrequency.AsNeeded),
            new("Damma hyllor", 2, TaskFrequency.AsNeeded),
            new("Byt handdukar", 1, TaskFrequency.Weekly),
            new("Dammsug golvet", 3, TaskFrequency.Weekly),
            new("Torka golvet", 5, TaskFrequency.Weekly)
        ]),
        new("Kitchen", "Kök",
        [
            new("Diska eller töm diskmaskinen", 8, TaskFrequency.Daily),
            new("Torka av bänkarna", 3, TaskFrequency.Daily),
            new("Rengör spisen", 5, TaskFrequency.Weekly),
            new("Töm soptunnan", 2, TaskFrequency.Weekly),
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Torka golvet", 5, TaskFrequency.Weekly)
        ]),
        new("Bedroom", "Sovrum",
        [
            new("Bädda sängen", 2, TaskFrequency.Daily),
            // Opening a window and closing it again - barely any active effort, but a daily
            // habit rather than something to be reminded of occasionally, so it keeps a token,
            // near-zero estimate rather than 0 and repeats daily alongside making the bed.
            new("Vädra rummet", 1, TaskFrequency.Daily),
            new("Dammsug golvet", 5, TaskFrequency.TwiceWeekly),
            new("Torka golvet", 3, TaskFrequency.Weekly),
            new("Damma ytor", 3, TaskFrequency.AsNeeded),
            new("Plocka undan kläder", 5, TaskFrequency.Weekly),
            new("Torka lister", 5, TaskFrequency.Monthly),
            // Ladders, reach, and a bit more care than most chores here - kept off children's
            // rotation by default; still fully editable per task afterwards.
            new("Tvätta fönster", 10, TaskFrequency.Monthly, AdultsOnly: true)
        ]),
        new("LivingRoom", "Vardagsrum",
        [
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Damma ytor", 5, TaskFrequency.AsNeeded),
            new("Plocka undan", 5, TaskFrequency.Weekly),
            new("Vädra rummet", 1, TaskFrequency.AsNeeded)
        ]),
        new("FamilyRoom", "Allrum",
        [
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Damma ytor", 5, TaskFrequency.AsNeeded),
            new("Plocka undan", 5, TaskFrequency.Weekly),
            new("Vädra rummet", 1, TaskFrequency.AsNeeded)
        ]),
        new("DiningRoom", "Matrum",
        [
            new("Torka av bordet", 2, TaskFrequency.Daily),
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Damma ytor", 3, TaskFrequency.AsNeeded)
        ]),
        new("Hallway", "Hall",
        [
            new("Dammsug eller sopa golvet", 3, TaskFrequency.Weekly),
            new("Torka golvet", 3, TaskFrequency.Weekly),
            new("Ställ i ordning skorna", 2, TaskFrequency.AsNeeded),
            new("Släng gammal post och reklam", 2, TaskFrequency.AsNeeded)
        ]),
        new("LaundryRoom", "Tvättstuga",
        [
            new("Dammsug golvet", 3, TaskFrequency.Weekly),
            new("Torka golvet", 3, TaskFrequency.Weekly),
            new("Töm luddfiltret i torktumlaren", 1, TaskFrequency.Weekly),
            new("Rengör tvättmaskinens tvättmedelsfack", 2, TaskFrequency.Monthly)
        ]),
        new("Office", "Kontor",
        [
            new("Dammsug golvet", 5, TaskFrequency.Weekly),
            new("Damma ytor", 3, TaskFrequency.AsNeeded),
            new("Plocka undan skrivbordet", 5, TaskFrequency.Weekly)
        ])
    ];
}
