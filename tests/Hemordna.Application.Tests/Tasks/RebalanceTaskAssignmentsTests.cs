using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

/// <summary>
/// Covers the 65/35 rebalance feature (docs/ARCHITECTURE.md, "65/35 target split"). Two members
/// are seeded with the SAME capacities the new role presets produce (245 and 455 minutes a
/// week - a 35%/65% split of their 700-minute combined total) throughout, so a plain assertion
/// that assigned minutes approach each member's <c>WeeklyTimeBudget</c> share is, for this
/// specific pair, also a direct check against the 65/35 target - without this test file (or the
/// production code it tests) ever comparing a role name.
/// </summary>
public class RebalanceTaskAssignmentsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private RebalanceTaskAssignments CreateUseCase()
        => new(_households, _definitions, _occurrences, _notifier);

    /// <summary>Anna: 35 min/day (245/week) - Bjorn: 65 min/day (455/week). The exact 7:13 split
    /// the new AdultFullTime/Retired presets produce.</summary>
    private async Task<(Guid HouseholdId, HouseholdMember Anna, HouseholdMember Bjorn)> ArrangeTwoMemberHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(35));
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(65), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, anna, bjorn);
    }

    private TaskOccurrence SeedRotatingOccurrence(
        Guid householdId, DateOnly date, int estimatedMinutes, Guid? assignedMemberId, bool requiresAdult = false)
    {
        var definition = TaskDefinition.Create(householdId, $"Uppgift {Guid.NewGuid()}", estimatedMinutes, Now);
        definition.SetRotatingResponsibility(true);
        definition.SetRequiresAdult(requiresAdult);
        _definitions.Seed(definition);

        var occurrence = definition.ScheduleFor(date, Now);

        if (assignedMemberId is { } memberId)
        {
            occurrence.AssignTo(memberId);
        }

        _occurrences.Seed(occurrence);
        return occurrence;
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var result = await CreateUseCase().HandleAsync(Guid.NewGuid(), Monday, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Reassigns_only_what_is_needed_to_approach_the_target_ratio_by_minutes()
    {
        // Five occurrences, all currently on Anna: 5, 5, 5, 5 and 60 minutes (80 total). Target
        // is 28 min for Anna / 52 for Bjorn (80 * 245/700 and 80 * 455/700). The 60-minute one
        // cannot fit Anna's 35-minute day at all, so it MUST move regardless of ratio; the four
        // 5-minute ones are freely movable and get split by running ratio as they are processed
        // in date order. Final split (10 Anna / 70 Bjorn) undershoots the target because the
        // single 60-minute block is indivisible - the closest achievable given the daily cap,
        // not the mathematical ideal - per "odelbara uppgifter" in the spec.
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        var occurrences = new[] { 5, 5, 5, 5, 60 }
            .Select((minutes, index) => SeedRotatingOccurrence(householdId, Monday.AddDays(index), minutes, anna.Id))
            .ToList();

        var changed = await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(3, changed);
        Assert.Equal(anna.Id, occurrences[0].AssignedMemberId);
        Assert.Equal(bjorn.Id, occurrences[1].AssignedMemberId);
        Assert.Equal(bjorn.Id, occurrences[2].AssignedMemberId);
        Assert.Equal(anna.Id, occurrences[3].AssignedMemberId);
        Assert.Equal(bjorn.Id, occurrences[4].AssignedMemberId);

        // Minutes, not task count: Anna ends up with MORE tasks (2) than her final minute total
        // (10) would suggest relative to Bjorn's fewer tasks (3) carrying far more time (70) -
        // a count-based split would never produce a 2-tasks/10-minutes vs 3-tasks/70-minutes
        // outcome from an even starting split of task sizes.
        Assert.Equal(10, occurrences.Where(o => o.AssignedMemberId == anna.Id).Sum(o => o.EstimatedMinutes));
        Assert.Equal(70, occurrences.Where(o => o.AssignedMemberId == bjorn.Id).Sum(o => o.EstimatedMinutes));

        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task Rerunning_immediately_after_a_rebalance_changes_nothing()
    {
        var (householdId, anna, _) = await ArrangeTwoMemberHouseholdAsync();

        foreach (var (minutes, index) in new[] { 5, 5, 5, 5, 60 }.Select((m, i) => (m, i)))
        {
            SeedRotatingOccurrence(householdId, Monday.AddDays(index), minutes, anna.Id);
        }

        var useCase = CreateUseCase();
        var firstRun = await useCase.HandleAsync(householdId, Monday, CancellationToken.None);
        Assert.True(firstRun > 0);

        var secondRun = await useCase.HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, secondRun);
        Assert.Equal(1, _notifier.CallCount); // only the first run's change notified, not a second empty one
    }

    [Fact]
    public async Task Keeps_the_current_split_when_it_is_already_the_closest_achievable()
    {
        // Two indivisible tasks - 90 minutes and 10 minutes - against a 35/65 target of 100
        // total. Every other possible arrangement (swap, or both on one person) has a strictly
        // larger total deviation from target than the current 90/10 split already has - see
        // RebalanceTaskAssignments's own worked example in code review. Nothing should move.
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        var big = SeedRotatingOccurrence(householdId, Monday, 90, bjorn.Id);
        var small = SeedRotatingOccurrence(householdId, Monday.AddDays(1), 10, anna.Id);

        var changed = await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, changed);
        Assert.Equal(bjorn.Id, big.AssignedMemberId);
        Assert.Equal(anna.Id, small.AssignedMemberId);
        Assert.Equal(0, _notifier.CallCount);
    }

    [Fact]
    public async Task Completed_skipped_and_archived_occurrences_are_never_touched()
    {
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        // A genuinely movable occurrence, so the run actually does something.
        var movable = SeedRotatingOccurrence(householdId, Monday, 60, anna.Id);

        var completedDefinition = TaskDefinition.Create(householdId, "Diska", 30, Now);
        completedDefinition.SetRotatingResponsibility(true);
        _definitions.Seed(completedDefinition);
        var completed = completedDefinition.ScheduleFor(Monday, Now);
        completed.AssignTo(bjorn.Id);
        completed.Complete(bjorn.Id, Now);
        _occurrences.Seed(completed);

        var skippedDefinition = TaskDefinition.Create(householdId, "Dammsug", 30, Now);
        skippedDefinition.SetRotatingResponsibility(true);
        _definitions.Seed(skippedDefinition);
        var skipped = skippedDefinition.ScheduleFor(Monday, Now);
        skipped.AssignTo(bjorn.Id);
        skipped.Skip();
        _occurrences.Seed(skipped);

        var archivedDefinition = TaskDefinition.Create(householdId, "Gammal uppgift", 30, Now);
        archivedDefinition.SetRotatingResponsibility(true);
        _definitions.Seed(archivedDefinition);
        var archived = archivedDefinition.ScheduleFor(Monday, Now);
        archived.AssignTo(bjorn.Id);
        archivedDefinition.Deactivate();
        _occurrences.Seed(archived);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(bjorn.Id, completed.AssignedMemberId);
        Assert.Equal(bjorn.Id, skipped.AssignedMemberId);
        Assert.Equal(bjorn.Id, archived.AssignedMemberId);
    }

    [Fact]
    public async Task A_fixed_non_rotating_task_is_never_reassigned()
    {
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        var fixedDefinition = TaskDefinition.Create(householdId, "Bädda sängen", 30, Now);
        fixedDefinition.SetDefaultResponsibleMember(bjorn.Id);
        _definitions.Seed(fixedDefinition);
        var fixedOccurrence = fixedDefinition.ScheduleFor(Monday, Now);
        _occurrences.Seed(fixedOccurrence);

        // A genuinely movable occurrence too, so the run actually does something and this is
        // not just a no-op test.
        SeedRotatingOccurrence(householdId, Monday, 60, anna.Id);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(bjorn.Id, fixedOccurrence.AssignedMemberId);
    }

    [Fact]
    public async Task An_adult_only_task_never_moves_to_a_child_and_moves_off_one_if_it_somehow_landed_there()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(35));
        anna.SetRole(HouseholdRole.AdultFullTime);
        var child = household.AddMember("Charlie", WeeklyTimeBudget.Uniform(65), Now.AddMinutes(1), HouseholdRole.ChildOrTeen);
        await _households.UpdateAsync(household, CancellationToken.None);

        // Data drift (or a role picked after the fact): an adults-only task is currently on the
        // child, despite the child's much higher capacity making them look "underused" by ratio.
        var occurrence = SeedRotatingOccurrence(household.Id, Monday, 20, child.Id, requiresAdult: true);

        var changed = await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        Assert.Equal(1, changed);
        Assert.Equal(anna.Id, occurrence.AssignedMemberId);
    }

    [Fact]
    public async Task A_member_paused_on_the_occurrences_date_never_keeps_it()
    {
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();
        bjorn.Pause(Monday.AddDays(7));

        var occurrence = SeedRotatingOccurrence(householdId, Monday, 20, bjorn.Id);

        var changed = await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(1, changed);
        Assert.Equal(anna.Id, occurrence.AssignedMemberId);
    }

    [Fact]
    public async Task The_result_is_deterministic_given_the_same_input_shape()
    {
        // Run the identical scenario twice, from scratch, with independent repositories and
        // freshly generated ids each time - a genuinely deterministic algorithm reaches the
        // same structural outcome (who ends up with how much, and how many moves it took) both
        // times, even though the two runs' Guids are never equal to each other.
        async Task<(int AnnaMinutes, int BjornMinutes, int Changed)> RunOnceAsync()
        {
            var households = new InMemoryHouseholdRepository();
            var definitions = new InMemoryTaskDefinitionRepository();
            var occurrences = new InMemoryTaskOccurrenceRepository();

            var household = await new CreateHousehold(households, new FixedTimeProvider(Now))
                .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
            var anna = household.Members.Single();
            anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(35));
            var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(65), Now.AddMinutes(1));
            await households.UpdateAsync(household, CancellationToken.None);

            var seeded = new List<TaskOccurrence>();

            foreach (var (minutes, index) in new[] { 5, 5, 5, 5, 60 }.Select((m, i) => (m, i)))
            {
                var definition = TaskDefinition.Create(household.Id, $"Uppgift {index}", minutes, Now);
                definition.SetRotatingResponsibility(true);
                definitions.Seed(definition);
                var occurrence = definition.ScheduleFor(Monday.AddDays(index), Now);
                occurrence.AssignTo(anna.Id);
                occurrences.Seed(occurrence);
                seeded.Add(occurrence);
            }

            var changed = await new RebalanceTaskAssignments(households, definitions, occurrences, new SpyHouseholdNotifier())
                .HandleAsync(household.Id, Monday, CancellationToken.None);

            var annaMinutes = seeded.Where(o => o.AssignedMemberId == anna.Id).Sum(o => o.EstimatedMinutes);
            var bjornMinutes = seeded.Where(o => o.AssignedMemberId == bjorn.Id).Sum(o => o.EstimatedMinutes);

            return (annaMinutes, bjornMinutes, changed!.Value);
        }

        var first = await RunOnceAsync();
        var second = await RunOnceAsync();

        Assert.Equal(first, second);
    }

    /// <summary>"Kvarlämnat stannar" - docs/ARCHITECTURE.md, "Beslut: Kvarlämnat, Imorgon på
    /// Idag, ledig dag och tid i förväg". An occurrence already overdue as of <c>today</c> is
    /// excluded from the movable pool entirely - its minutes never enter the ratio math at all,
    /// so it stays with whoever already has it no matter how lopsided that leaves the split;
    /// only the genuinely on-time occurrence is free to move.</summary>
    [Fact]
    public async Task A_task_that_is_already_overdue_never_changes_owner_even_when_the_split_is_skewed()
    {
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        // Anna (lower capacity, 35 min/day) already has a 60-minute task overdue from last
        // week - it is never even considered movable, so it plays no part in the ratio below.
        // Bjorn (higher capacity) has a single 5-minute task due today.
        var overdue = SeedRotatingOccurrence(householdId, Monday.AddDays(-7), 60, anna.Id);
        var dueToday = SeedRotatingOccurrence(householdId, Monday, 5, bjorn.Id);

        var changed = await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        // The overdue task never moves - it was never a candidate. The one movable task (5 min)
        // is too small to create a strict ratio advantage either way, so ties favor keeping its
        // current owner too - nothing changes.
        Assert.Equal(anna.Id, overdue.AssignedMemberId);
        Assert.Equal(bjorn.Id, dueToday.AssignedMemberId);
        Assert.Equal(0, changed);
    }

    /// <summary>The mirror case: an occurrence due exactly today (never deferred, never late)
    /// is still fully movable, same as before this feature existed.</summary>
    [Fact]
    public async Task A_task_due_today_is_still_movable()
    {
        var (householdId, anna, bjorn) = await ArrangeTwoMemberHouseholdAsync();

        // A single, indivisible 60-minute task, currently on Anna (35 min/day - far too little
        // room for it), due exactly today. With nobody else currently holding any movable work,
        // Bjorn (65 min/day) is the only legal home for it.
        var occurrence = SeedRotatingOccurrence(householdId, Monday, 60, anna.Id);

        var changed = await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(1, changed);
        Assert.Equal(bjorn.Id, occurrence.AssignedMemberId);
    }
}
