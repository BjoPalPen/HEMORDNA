using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

public class WeeklyPlacementPlannerTests
{
    private readonly WeeklyPlacementPlanner _planner = new();

    private static IReadOnlyList<WeekdayCapacity> UniformCapacity(int minutes, TaskEffort maxEffort = TaskEffort.Heavy)
        => Enum.GetValues<DayOfWeek>().Select(day => new WeekdayCapacity(day, minutes, maxEffort)).ToList();

    private static PlaceableVisit Visit(
        int minutes,
        VisitKind kind = VisitKind.RegularClean,
        TaskEffort requiredEffort = TaskEffort.Medium,
        Guid? areaId = null,
        Guid? taskId = null)
        => new(
            areaId ?? Guid.NewGuid(),
            "Rum",
            kind,
            minutes,
            requiredEffort,
            [taskId ?? Guid.NewGuid()]);

    [Fact]
    public void No_visits_leaves_every_weekday_untouched()
    {
        var request = new WeeklyPlacementRequest(UniformCapacity(30), []);

        var result = _planner.Plan(request);

        Assert.Empty(result.PlacedVisits);
        Assert.All(Enum.GetValues<DayOfWeek>(), day => Assert.Equal(30, result.MinutesBeforeByDay[day]));
        Assert.All(Enum.GetValues<DayOfWeek>(), day => Assert.Equal(30, result.MinutesAfterByDay[day]));
    }

    [Fact]
    public void A_single_visit_goes_to_the_weekday_with_the_most_remaining_minutes()
    {
        var weekdays = UniformCapacity(30).Select(w =>
            w.Day == DayOfWeek.Thursday ? w with { AvailableMinutes = 90 } : w).ToList();
        var visit = Visit(20);

        var result = _planner.Plan(new WeeklyPlacementRequest(weekdays, [visit]));

        var placement = Assert.Single(result.PlacedVisits);
        Assert.Equal(DayOfWeek.Thursday, placement.Day);
        Assert.Equal(70, result.MinutesAfterByDay[DayOfWeek.Thursday]);
    }

    [Fact]
    public void Ties_break_to_monday_first()
    {
        var visit = Visit(10);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(30), [visit]));

        Assert.Equal(DayOfWeek.Monday, Assert.Single(result.PlacedVisits).Day);
    }

    [Fact]
    public void Heavier_visits_are_placed_before_lighter_ones_regardless_of_size()
    {
        // A small DeepClean should still be placed (and therefore "claim" the best day) before a
        // much bigger RegularClean - see docs/ARCHITECTURE.md "tyngst först".
        var heavy = Visit(10, VisitKind.DeepClean, TaskEffort.Heavy);
        var big = Visit(60, VisitKind.RegularClean, TaskEffort.Medium);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(100), [big, heavy]));

        // Since capacity is uniform, the heavy visit (placed first) claims Monday; the big
        // regular visit, placed second, sees Monday now has less room than the rest and lands
        // on Tuesday instead (still tied among Tue..Sun, so Monday's successor wins).
        var heavyPlacement = result.PlacedVisits.Single(p => p.Visit == heavy);
        var bigPlacement = result.PlacedVisits.Single(p => p.Visit == big);
        Assert.Equal(DayOfWeek.Monday, heavyPlacement.Day);
        Assert.Equal(DayOfWeek.Tuesday, bigPlacement.Day);
    }

    [Fact]
    public void Among_equally_heavy_visits_the_biggest_one_is_placed_first()
    {
        var small = Visit(10);
        var big = Visit(50);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(100), [small, big]));

        // Big placed first claims Monday (the emptiest day at that point); small, placed second,
        // now finds every OTHER day still has the full 100 while Monday has only 50 left, so it
        // goes to Tuesday.
        Assert.Equal(DayOfWeek.Monday, result.PlacedVisits.Single(p => p.Visit == big).Day);
        Assert.Equal(DayOfWeek.Tuesday, result.PlacedVisits.Single(p => p.Visit == small).Day);
    }

    [Fact]
    public void A_deep_clean_only_goes_to_a_weekday_whose_ceiling_allows_heavy()
    {
        var weekdays = UniformCapacity(30, TaskEffort.Medium)
            .Select(w => w.Day == DayOfWeek.Saturday ? w with { MaxEffort = TaskEffort.Heavy } : w)
            .ToList();
        var visit = Visit(20, VisitKind.DeepClean, TaskEffort.Heavy);

        var result = _planner.Plan(new WeeklyPlacementRequest(weekdays, [visit]));

        Assert.Equal(DayOfWeek.Saturday, Assert.Single(result.PlacedVisits).Day);
    }

    [Fact]
    public void When_no_weekday_allows_the_required_effort_it_still_gets_placed()
    {
        // Nobody's ceiling ever reaches Heavy any day of the week - the visit still needs a
        // home, same "always assign, never drop" fallback RotationPicker already uses.
        var weekdays = UniformCapacity(30, TaskEffort.Medium);
        var visit = Visit(20, VisitKind.DeepClean, TaskEffort.Heavy);

        var result = _planner.Plan(new WeeklyPlacementRequest(weekdays, [visit]));

        Assert.Single(result.PlacedVisits);
        Assert.Equal(DayOfWeek.Monday, result.PlacedVisits[0].Day);
    }

    [Fact]
    public void The_result_does_not_depend_on_the_order_visits_arrive_in()
    {
        var visits = new[]
        {
            Visit(10, VisitKind.RegularClean, TaskEffort.Light, taskId: Guid.Parse("00000000-0000-0000-0000-000000000001")),
            Visit(40, VisitKind.DeepClean, TaskEffort.Heavy, taskId: Guid.Parse("00000000-0000-0000-0000-000000000002")),
            Visit(25, VisitKind.RegularClean, TaskEffort.Medium, taskId: Guid.Parse("00000000-0000-0000-0000-000000000003"))
        };

        var forward = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(60), visits));
        var reversed = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(60), visits.Reverse().ToArray()));

        var forwardByTask = forward.PlacedVisits.ToDictionary(p => p.Visit.TaskDefinitionIds[0], p => p.Day);
        var reversedByTask = reversed.PlacedVisits.ToDictionary(p => p.Visit.TaskDefinitionIds[0], p => p.Day);
        Assert.Equal(forwardByTask, reversedByTask);
    }

    [Fact]
    public void Rejects_a_weekday_list_that_is_not_exactly_one_entry_per_day()
    {
        var incomplete = UniformCapacity(30).Take(6).ToList();

        Assert.Throws<ArgumentException>(
            () => _planner.Plan(new WeeklyPlacementRequest(incomplete, [])));
    }

    /// <summary>
    /// Björns produktionsdata: en skev vecka (128, 120, 33, 128, 25, 29, 97 minuter/dag) uppstod
    /// för att varje rum fick "nästa veckodag i tur" oavsett storlek - se docs/ARCHITECTURE.md.
    /// Med den giriga algoritmen, minst lika mycket kapacitet varje dag, sprids fem rum av olika
    /// storlek betydligt jämnare än ren turordning skulle ge.
    /// </summary>
    [Fact]
    public void Rooms_of_different_sizes_spread_more_evenly_than_naive_round_robin_would()
    {
        var weekdays = UniformCapacity(60);
        var visits = new[]
        {
            Visit(40, taskId: Guid.NewGuid()), // Badrum
            Visit(15, taskId: Guid.NewGuid()), // Litet wc
            Visit(35, taskId: Guid.NewGuid()), // Kök
            Visit(10, taskId: Guid.NewGuid()), // Hall
            Visit(20, taskId: Guid.NewGuid())  // Sovrum
        };

        var result = _planner.Plan(new WeeklyPlacementRequest(weekdays, visits));

        // Every visit lands on its own weekday - no naive "everyone piles onto Monday" outcome,
        // and no day is left carrying two rooms while others carry none.
        var days = result.PlacedVisits.Select(p => p.Day).ToList();
        Assert.Equal(days.Count, days.Distinct().Count());
    }
}
