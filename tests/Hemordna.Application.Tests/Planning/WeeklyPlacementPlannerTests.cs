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
        Guid? taskId = null,
        DayOfWeek? lockedWeekday = null)
        => new(
            areaId ?? Guid.NewGuid(),
            "Rum",
            kind,
            minutes,
            requiredEffort,
            [taskId ?? Guid.NewGuid()],
            lockedWeekday);

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

    // "Alltid på en viss veckodag" - Björns krav. See docs/ARCHITECTURE.md "Beslut: Alltid på en
    // viss veckodag".

    [Fact]
    public void A_locked_visit_lands_on_its_own_weekday_and_is_counted_into_capacity_first()
    {
        var locked = Visit(40, lockedWeekday: DayOfWeek.Tuesday);
        // An unlocked visit would otherwise prefer Tuesday too (most remaining minutes) - but
        // the locked visit already claimed 40 of Tuesday's 60, so the unlocked one goes
        // elsewhere instead, proving the locked visit's minutes were counted BEFORE the greedy
        // pass ran, not just placed alongside it.
        var unlocked = Visit(30);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(60), [locked, unlocked]));

        Assert.Equal(DayOfWeek.Tuesday, result.PlacedVisits.Single(p => p.Visit == locked).Day);
        Assert.NotEqual(DayOfWeek.Tuesday, result.PlacedVisits.Single(p => p.Visit == unlocked).Day);
        Assert.Equal(20, result.MinutesAfterByDay[DayOfWeek.Tuesday]);
    }

    [Fact]
    public void A_locked_visit_is_placed_on_its_day_even_when_nobody_has_the_ceiling_for_its_effort()
    {
        // Every weekday caps at Medium - a Heavy visit would normally fall back to "every
        // weekday is a candidate" and pick the emptiest one (Monday). Locked to Thursday, the
        // requirement wins outright - no effort check at all.
        var weekdays = UniformCapacity(30, TaskEffort.Medium);
        var locked = Visit(20, VisitKind.DeepClean, TaskEffort.Heavy, lockedWeekday: DayOfWeek.Thursday);

        var result = _planner.Plan(new WeeklyPlacementRequest(weekdays, [locked]));

        Assert.Equal(DayOfWeek.Thursday, Assert.Single(result.PlacedVisits).Day);
    }

    [Fact]
    public void A_visit_with_exactly_one_locked_task_takes_the_whole_visit_with_it()
    {
        // Two tasks in the same room/kind, only one locked - the room rule holds, so the
        // UNLOCKED task's minutes count too, and the whole thing goes to the locked day.
        var areaId = Guid.NewGuid();
        var visit = new PlaceableVisit(
            areaId, "Badrum", VisitKind.RegularClean, 50, TaskEffort.Medium,
            [Guid.NewGuid(), Guid.NewGuid()], DayOfWeek.Wednesday);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(60), [visit]));

        var placement = Assert.Single(result.PlacedVisits);
        Assert.Equal(DayOfWeek.Wednesday, placement.Day);
        Assert.Equal(10, result.MinutesAfterByDay[DayOfWeek.Wednesday]);
    }

    [Fact]
    public void Locked_visits_are_placed_before_the_greedy_pass_regardless_of_how_light_they_are()
    {
        // A tiny locked visit and a big unlocked one - if the locked visit were run through the
        // normal "heaviest/biggest first" ordering it would lose to the big one and never get
        // priority. It must still claim its day unconditionally.
        var locked = Visit(5, VisitKind.RegularClean, TaskEffort.Light, lockedWeekday: DayOfWeek.Friday);
        var big = Visit(50, VisitKind.DeepClean, TaskEffort.Heavy);

        var result = _planner.Plan(new WeeklyPlacementRequest(UniformCapacity(60), [big, locked]));

        Assert.Equal(DayOfWeek.Friday, result.PlacedVisits.Single(p => p.Visit == locked).Day);
    }
}
