using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

/// <summary>
/// Builder-level coverage for "Alltid på en viss veckodag" (Björns krav) - how
/// <see cref="WeeklyPlacementBuilder"/> turns a room's locked/unlocked tasks into visits, end to
/// end through the real <see cref="PreviewWeeklyPlan"/> use case rather than constructing
/// <see cref="PlaceableVisit"/> by hand (see <c>WeeklyPlacementPlannerTests</c> for that level).
/// </summary>
public class PreviewWeeklyPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Wednesday = new(2026, 3, 4);
    private static readonly DateOnly OldFriday = new(2026, 2, 27);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private PreviewWeeklyPlan CreateUseCase() => new(_households, _definitions);

    private async Task<(Household Household, HouseholdMember Anna, Guid BathroomId)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        var bathroom = household.AddArea("Badrum");
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household, anna, bathroom.Id);
    }

    [Fact]
    public async Task A_room_with_exactly_one_locked_task_places_the_whole_visit_on_that_day()
    {
        var (household, anna, bathroomId) = await ArrangeHouseholdAsync();

        var lockedTask = TaskDefinition.Create(household.Id, "Skrubba handfatet", 20, Now);
        lockedTask.AssignToArea(bathroomId);
        lockedTask.SetDefaultResponsibleMember(anna.Id);
        lockedTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Thursday));
        lockedTask.SetPreferredWeekday(DayOfWeek.Thursday);
        _definitions.Seed(lockedTask);

        var unlockedTask = TaskDefinition.Create(household.Id, "Torka golvet", 15, Now);
        unlockedTask.AssignToArea(bathroomId);
        unlockedTask.SetDefaultResponsibleMember(anna.Id);
        unlockedTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        _definitions.Seed(unlockedTask);

        var result = await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);

        // One visit, both tasks, on the locked task's own day - the room rule held them together.
        var placement = Assert.Single(result!.Placement.PlacedVisits);
        Assert.Equal(DayOfWeek.Thursday, placement.Day);
        Assert.Equal(2, placement.Visit.TaskDefinitionIds.Count);
        Assert.Equal(35, placement.Visit.Minutes);
    }

    [Fact]
    public async Task A_room_with_two_different_locked_days_splits_into_separate_visits()
    {
        var (household, anna, bathroomId) = await ArrangeHouseholdAsync();

        var lockedTuesday = TaskDefinition.Create(household.Id, "Byt handduk", 10, Now);
        lockedTuesday.AssignToArea(bathroomId);
        lockedTuesday.SetDefaultResponsibleMember(anna.Id);
        lockedTuesday.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Tuesday));
        lockedTuesday.SetPreferredWeekday(DayOfWeek.Tuesday);
        _definitions.Seed(lockedTuesday);

        var lockedFriday = TaskDefinition.Create(household.Id, "Skrubba handfatet", 20, Now);
        lockedFriday.AssignToArea(bathroomId);
        lockedFriday.SetDefaultResponsibleMember(anna.Id);
        lockedFriday.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        lockedFriday.SetPreferredWeekday(DayOfWeek.Friday);
        _definitions.Seed(lockedFriday);

        var unlockedTask = TaskDefinition.Create(household.Id, "Torka golvet", 15, Now);
        unlockedTask.AssignToArea(bathroomId);
        unlockedTask.SetDefaultResponsibleMember(anna.Id);
        unlockedTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        _definitions.Seed(unlockedTask);

        var result = await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);

        // Three separate visits: each locked task on its own day, the unlocked task forming its
        // own visit placed by the ordinary greedy algorithm (lands on whichever weekday has the
        // most remaining capacity once the two locked visits are counted in).
        Assert.Equal(3, result!.Placement.PlacedVisits.Count);

        var tuesdayPlacement = result.Placement.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(lockedTuesday.Id));
        Assert.Equal(DayOfWeek.Tuesday, tuesdayPlacement.Day);
        Assert.Single(tuesdayPlacement.Visit.TaskDefinitionIds);

        var fridayPlacement = result.Placement.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(lockedFriday.Id));
        Assert.Equal(DayOfWeek.Friday, fridayPlacement.Day);
        Assert.Single(fridayPlacement.Visit.TaskDefinitionIds);

        var unlockedPlacement = result.Placement.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(unlockedTask.Id));
        Assert.Single(unlockedPlacement.Visit.TaskDefinitionIds);
        Assert.DoesNotContain(unlockedPlacement.Visit.TaskDefinitionIds, id => id == lockedTuesday.Id || id == lockedFriday.Id);
    }

    /// <summary>
    /// "Planera veckan går att ändra" (Björns krav) - a move is treated exactly like an existing
    /// PreferredWeekday lock: placed first, unconditionally, on the chosen day, and the rest of
    /// the week spreads around it. A single-task room visit's key equals that task's own id (see
    /// PlaceableVisit.VisitKey), so the two rooms' own ids can be used directly as move keys here.
    /// </summary>
    [Fact]
    public async Task A_move_places_the_visit_on_the_chosen_day_and_the_rest_spreads_around_it()
    {
        var (household, anna, bathroomId) = await ArrangeHouseholdAsync();
        var kitchen = household.AddArea("Kök");
        await _households.UpdateAsync(household, CancellationToken.None);

        var kitchenTask = TaskDefinition.Create(household.Id, "Städa köket", 40, Now);
        kitchenTask.AssignToArea(kitchen.Id);
        kitchenTask.ChangeEffort(TaskEffort.Medium);
        kitchenTask.SetDefaultResponsibleMember(anna.Id);
        kitchenTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(kitchenTask);

        var bathroomTask = TaskDefinition.Create(household.Id, "Skrubba handfatet", 20, Now);
        bathroomTask.AssignToArea(bathroomId);
        bathroomTask.ChangeEffort(TaskEffort.Medium);
        bathroomTask.SetDefaultResponsibleMember(anna.Id);
        bathroomTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(bathroomTask);

        // Utan någon flytt: den större (Kök, 40 min) vinner Måndag i den giriga ordningen, den
        // mindre (Badrum, 20 min) hamnar på Tisdag - samma mönster som ApplyWeeklyPlanTests.
        var withoutMove = await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);
        var kitchenDefaultDay = withoutMove!.Placement.PlacedVisits
            .Single(p => p.Visit.TaskDefinitionIds.Contains(kitchenTask.Id)).Day;
        Assert.Equal(DayOfWeek.Monday, kitchenDefaultDay);

        // Flytta badrumsbesöket till Måndag - det ska nu ta Måndag ovillkorligt, och köket
        // (fortfarande olåst) ska spridas till en ANNAN dag runt det.
        var moves = new Dictionary<Guid, DayOfWeek> { [bathroomTask.Id] = DayOfWeek.Monday };
        var withMove = await CreateUseCase().HandleAsync(household.Id, Wednesday, moves, CancellationToken.None);

        var bathroomPlacement = withMove!.Placement.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(bathroomTask.Id));
        var kitchenPlacement = withMove.Placement.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(kitchenTask.Id));

        Assert.Equal(DayOfWeek.Monday, bathroomPlacement.Day);
        Assert.NotEqual(DayOfWeek.Monday, kitchenPlacement.Day);
        Assert.Empty(withMove.EffortWarningDays);
    }

    /// <summary>
    /// Flytt till en dag där ingen orkar besökets tyngd är tillåten (kravet vinner, precis som ett
    /// befintligt lås) - men en lugn notis ska visas i stället för tystnad. Ingen ork-kontroll för
    /// låsta/flyttade besök, se WeeklyPlacementPlanner.
    /// </summary>
    [Fact]
    public async Task Moving_a_heavy_visit_to_a_day_nobody_can_handle_still_places_it_there_and_warns()
    {
        var (household, anna, bathroomId) = await ArrangeHouseholdAsync();
        anna.ChangeWeeklyEffortCeiling(WeeklyEffortCeiling.Create(
            Enum.GetValues<DayOfWeek>().ToDictionary(
                day => day, day => day == DayOfWeek.Tuesday ? TaskEffort.Light : TaskEffort.Heavy)));
        await _households.UpdateAsync(household, CancellationToken.None);

        var heavyTask = TaskDefinition.Create(household.Id, "Storstäda badrummet", 40, Now);
        heavyTask.AssignToArea(bathroomId);
        heavyTask.ChangeEffort(TaskEffort.Heavy);
        heavyTask.SetDefaultResponsibleMember(anna.Id);
        heavyTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(heavyTask);

        var moves = new Dictionary<Guid, DayOfWeek> { [heavyTask.Id] = DayOfWeek.Tuesday };
        var result = await CreateUseCase().HandleAsync(household.Id, Wednesday, moves, CancellationToken.None);

        var placement = Assert.Single(result!.Placement.PlacedVisits);
        Assert.Equal(DayOfWeek.Tuesday, placement.Day);
        Assert.Contains(DayOfWeek.Tuesday, result.EffortWarningDays);
    }
}
