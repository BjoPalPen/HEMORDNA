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

        var result = await CreateUseCase().HandleAsync(household.Id, Wednesday, CancellationToken.None);

        // One visit, both tasks, on the locked task's own day - the room rule held them together.
        var placement = Assert.Single(result!.PlacedVisits);
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

        var result = await CreateUseCase().HandleAsync(household.Id, Wednesday, CancellationToken.None);

        // Three separate visits: each locked task on its own day, the unlocked task forming its
        // own visit placed by the ordinary greedy algorithm (lands on whichever weekday has the
        // most remaining capacity once the two locked visits are counted in).
        Assert.Equal(3, result!.PlacedVisits.Count);

        var tuesdayPlacement = result.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(lockedTuesday.Id));
        Assert.Equal(DayOfWeek.Tuesday, tuesdayPlacement.Day);
        Assert.Single(tuesdayPlacement.Visit.TaskDefinitionIds);

        var fridayPlacement = result.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(lockedFriday.Id));
        Assert.Equal(DayOfWeek.Friday, fridayPlacement.Day);
        Assert.Single(fridayPlacement.Visit.TaskDefinitionIds);

        var unlockedPlacement = result.PlacedVisits.Single(p => p.Visit.TaskDefinitionIds.Contains(unlockedTask.Id));
        Assert.Single(unlockedPlacement.Visit.TaskDefinitionIds);
        Assert.DoesNotContain(unlockedPlacement.Visit.TaskDefinitionIds, id => id == lockedTuesday.Id || id == lockedFriday.Id);
    }
}
