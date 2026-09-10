using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class EnsureOccurrencesGeneratedTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly InMemoryMemberTimeCreditRepository _credits = new();

    private EnsureOccurrencesGenerated CreateUseCase()
        => new(_households, _definitions, _occurrences, _assignments, _daysOff, _credits, new FixedTimeProvider(Now));

    private async Task<Guid> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return household.Id;
    }

    [Fact]
    public async Task Generates_the_first_occurrence_once_it_is_due()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Equal(Monday, lastDate);
    }

    [Fact]
    public async Task Does_not_generate_ahead_of_when_the_rule_is_due()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Weekly(Monday, DayOfWeek.Friday));
        _definitions.Seed(definition);

        // Today is Monday; the rule is not due until Friday.
        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Null(lastDate);
    }

    [Fact]
    public async Task Catches_up_every_missed_occurrence_up_to_today()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);

        // Three days have passed with nobody opening the app.
        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(3), CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Equal(Monday.AddDays(3), lastDate);
        Assert.Equal(4, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Calling_it_twice_on_the_same_day_does_not_duplicate()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);
        var useCase = CreateUseCase();

        await useCase.HandleAsync(householdId, Monday, CancellationToken.None);
        await useCase.HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Ignores_a_definition_without_a_recurrence_rule()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Engångsstädning", 20, Now);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Ignores_an_inactive_definition_even_with_a_recurrence_rule()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.Deactivate();
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Generates_an_as_needed_occurrence_once_it_is_stale()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);

        // Never completed: the interval counts from creation (Monday).
        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(14), CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Does_not_generate_an_as_needed_occurrence_before_it_is_stale()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(13), CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task An_as_needed_task_does_not_pile_up_a_second_occurrence_while_one_is_outstanding()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);
        var useCase = CreateUseCase();

        await useCase.HandleAsync(householdId, Monday.AddDays(14), CancellationToken.None);
        await useCase.HandleAsync(householdId, Monday.AddDays(20), CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task An_as_needed_task_becomes_due_again_from_its_last_completion_not_its_creation()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);

        // Completed 5 days after creation, so the next due date is 5 + 14 days out - not 14
        // days from creation, which would wrongly ignore the completion.
        var completedAt = Now.AddDays(5);
        var doneOccurrence = definition.ScheduleFor(Monday.AddDays(5), Now);
        doneOccurrence.Complete(Guid.NewGuid(), completedAt);
        _occurrences.Seed(doneOccurrence);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(5 + 13), CancellationToken.None);
        Assert.Equal(0, _occurrences.AddCallCount);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(5 + 14), CancellationToken.None);
        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task A_rotating_recurring_task_assigns_and_records_a_turn_for_each_generated_occurrence()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        definition.SetDefaultResponsibleMember(anna.Id);
        _definitions.Seed(definition);

        // Two days due: rotation should hand Monday to Anna and Tuesday to Bjorn.
        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(1), CancellationToken.None);

        Assert.Equal(2, _assignments.Count);
        var last = await _assignments.FindMostRecentAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(bjorn.Id, last!.MemberId);
    }

    [Fact]
    public async Task Many_new_rotating_tasks_created_together_split_evenly_between_equally_available_members()
    {
        // The actual production bug this fixes: setting up Områden creates dozens of rotating
        // task definitions in one sitting, all due on the same day with no assignment history
        // yet - the old "no history -> earliest-joined member" fallback sent every single one
        // of them to the same person. See docs/ARCHITECTURE.md §6.
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(30));
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(30), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            var definition = TaskDefinition.Create(household.Id, $"Uppgift {i}", 20, Now);
            definition.SetRecurrence(RecurrenceRule.Daily(Monday));
            definition.SetRotatingResponsibility(true);
            _definitions.Seed(definition);
        }

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var totals = await _assignments.GetAssignedMinutesByMemberAsync(household.Id, CancellationToken.None);
        Assert.Equal(100, totals[anna.Id]);
        Assert.Equal(100, totals[bjorn.Id]);
    }

    [Fact]
    public async Task Rotation_favors_the_member_with_more_available_time()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(60 / 7)); // works full time: little time to spare
        var bjorn = household.AddMember(
            "Bjorn", WeeklyTimeBudget.Uniform(300 / 7), Now.AddMinutes(1)); // retired: five times the free time
        await _households.UpdateAsync(household, CancellationToken.None);

        for (var i = 0; i < 10; i++)
        {
            var definition = TaskDefinition.Create(household.Id, $"Uppgift {i}", 20, Now);
            definition.SetRecurrence(RecurrenceRule.Daily(Monday));
            definition.SetRotatingResponsibility(true);
            _definitions.Seed(definition);
        }

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var totals = await _assignments.GetAssignedMinutesByMemberAsync(household.Id, CancellationToken.None);

        // Five times the free time should mean noticeably more of the rotating work - not
        // exactly 5x (the picks are discrete, and both start tied at zero), but not a close
        // split either.
        Assert.True(totals[bjorn.Id] > totals.GetValueOrDefault(anna.Id));
        Assert.True(totals[bjorn.Id] >= totals.GetValueOrDefault(anna.Id) * 2);
    }

    [Fact]
    public async Task A_large_historical_imbalance_does_not_dump_an_entire_days_backlog_on_the_underused_member()
    {
        // The actual production bug this fixes: a household that had gone badly lopsided
        // before the fairness fix existed (see the two tests above) has one member sitting at
        // a far lower all-time ratio than the other. Setting up many rotating tasks at once
        // funnelled EVERY one of them to that single under-served member - even though they had
        // much LESS time to spare that day than the other member - because ratio alone says
        // nothing about whether today is realistic for them.
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(30)); // little daily time to spare
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(60), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        // A large pre-existing imbalance: Bjorn has done a lot, Anna nothing - Anna's all-time
        // ratio is now far below Bjorn's.
        await _assignments.AddAsync(
            TaskAssignment.Create(household.Id, Guid.NewGuid(), bjorn.Id, Monday.AddDays(-7), Now, 300),
            CancellationToken.None);

        // Three tasks, 60 minutes total - comfortably within Anna and Bjorn's COMBINED daily
        // capacity (30 + 60 = 90), so the daily cap can do its job without either of them ever
        // being forced over budget just because the total happens to be more than one person's
        // day can hold.
        for (var i = 0; i < 3; i++)
        {
            var definition = TaskDefinition.Create(household.Id, $"Uppgift {i}", 20, Now);
            definition.SetRecurrence(RecurrenceRule.Daily(Monday));
            definition.SetRotatingResponsibility(true);
            _definitions.Seed(definition);
        }

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var totals = await _assignments.GetAssignedMinutesByMemberOnDateAsync(household.Id, Monday, CancellationToken.None);

        // Anna's Monday budget is 30 minutes - room for exactly one 20-minute task, not two.
        // Everything past that must go to Bjorn instead, even though Anna's cumulative ratio
        // is still far lower.
        Assert.Equal(20, totals.GetValueOrDefault(anna.Id));
        Assert.Equal(40, totals.GetValueOrDefault(bjorn.Id));
    }

    [Fact]
    public async Task Does_not_generate_a_duplicate_for_a_date_an_existing_occurrence_was_moved_onto()
    {
        // Simulates RebalanceSchedule re-anchoring a definition's recurrence to a new weekday
        // AFTER it already had an outstanding occurrence, which RebalanceSchedule moves
        // (DeferTo) onto the new weekday rather than duplicating. FindMostRecentOriginalDateAsync
        // still reports the OLD, immutable original date as the cursor, so without a guard the
        // generator would not know the new weekday is already covered and would create a second,
        // genuinely duplicate occurrence for it once that date arrives.
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Torka golvet", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Weekly(Monday, DayOfWeek.Monday));
        _definitions.Seed(definition);

        var original = definition.ScheduleFor(Monday, Now);
        _occurrences.Seed(original);

        // Re-anchor to Wednesday, and move the existing occurrence onto it - exactly what
        // RebalanceSchedule does.
        definition.SetRecurrence(RecurrenceRule.Weekly(Monday, DayOfWeek.Wednesday));
        original.DeferTo(Monday.AddDays(2));

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(2), CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task A_paused_household_generates_nothing_and_catches_up_nothing_after_resuming()
    {
        var householdId = await ArrangeHouseholdAsync();
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        household!.Pause(Monday.AddDays(2));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);

        // Household paused Mon-Tue; the household opens the app again on Wednesday, once the
        // pause has already lifted.
        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(2), CancellationToken.None);
        Assert.Equal(0, _occurrences.AddCallCount);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(3), CancellationToken.None);

        // Only Wednesday - Monday and Tuesday were skipped for good, not queued up as a backlog.
        Assert.Equal(1, _occurrences.AddCallCount);
        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Equal(Monday.AddDays(3), lastDate);
    }

    [Fact]
    public async Task A_paused_rooms_task_generates_nothing_and_catches_up_nothing_after_resuming()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var area = household.AddArea("Badrum");
        area.Pause(Monday.AddDays(2));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Skrubba dusch", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.AssignToArea(area.Id);
        _definitions.Seed(definition);

        // The room is paused Mon-Tue; the household opens the app again on Wednesday, once the
        // pause has already lifted.
        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(2), CancellationToken.None);
        Assert.Equal(0, _occurrences.AddCallCount);

        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(3), CancellationToken.None);

        // Only Wednesday - Monday and Tuesday were skipped for good, not queued up as a backlog.
        Assert.Equal(1, _occurrences.AddCallCount);
        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(Monday.AddDays(3), lastDate);
    }

    [Fact]
    public async Task A_paused_rooms_rotating_task_is_skipped_entirely_not_just_reassigned()
    {
        // Unlike a single paused member, whose rotating task simply goes to someone else (see
        // A_paused_member_is_skipped_by_rotation_but_the_task_still_goes_to_someone_else below),
        // nobody can stand in for a room nobody can use - the task must not be generated for
        // ANY member while its own room is paused.
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now.AddMinutes(1));
        var area = household.AddArea("Badrum");
        area.Pause(Monday.AddDays(2));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Skrubba dusch", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        definition.AssignToArea(area.Id);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task A_paused_member_is_skipped_by_rotation_but_the_task_still_goes_to_someone_else()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now.AddMinutes(1));
        anna.Pause(Monday.AddDays(10));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var last = await _assignments.FindMostRecentAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(bjorn.Id, last!.MemberId);
    }

    [Fact]
    public async Task A_fixed_tasks_new_occurrences_stop_while_its_owner_is_paused()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.Pause(Monday.AddDays(2));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Betala räkningar", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetDefaultResponsibleMember(anna.Id);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(2), CancellationToken.None);
        Assert.Equal(0, _occurrences.AddCallCount);

        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(3), CancellationToken.None);
        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task A_member_on_a_day_off_is_never_picked_even_when_everyone_else_is_full()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        // Bjorn has almost no room today - if the "nobody has room" fallback fell back to
        // EVERY active member rather than just the eligible ones, Anna (off, but otherwise
        // "eligible" by every other rule) could still end up picked here.
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(5), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        _daysOff.Seed(MemberDayOff.Create(household.Id, anna.Id, Monday, Monday));

        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var last = await _assignments.FindMostRecentAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(bjorn.Id, last!.MemberId);
    }

    [Fact]
    public async Task A_fixed_tasks_owner_being_off_pushes_it_to_their_own_next_free_day_never_someone_else()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(60), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        _daysOff.Seed(MemberDayOff.Create(household.Id, anna.Id, Monday, Monday));

        var definition = TaskDefinition.Create(household.Id, "Betala räkningar", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetDefaultResponsibleMember(anna.Id);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var occurrence = Assert.Single(await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None));
        Assert.Equal(anna.Id, occurrence.AssignedMemberId);
        Assert.Equal(Monday, occurrence.OriginalScheduledDate);
        Assert.Equal(Monday.AddDays(1), occurrence.ScheduledDate);
    }

    [Fact]
    public async Task Outstanding_time_credit_biases_the_pick_toward_the_other_member_and_records_exactly_one_consumed_row()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(60));
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(60), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        // Equal capacities, zero prior assignments - without credit this would be a tie, broken
        // in Anna's favour (created first). 30 minutes of Anna's own credit is enough to make
        // her look more loaded than Bjorn for this 20-minute task.
        _credits.Seed(MemberTimeCredit.Earned(household.Id, anna.Id, Monday, TimeCreditReason.WorkedAhead, 30, Guid.NewGuid()));

        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var last = await _assignments.FindMostRecentAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(bjorn.Id, last!.MemberId);

        var occurrence = Assert.Single(await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None));
        var consumedRows = await _credits.ListForMemberAsync(household.Id, anna.Id, Monday, Monday, CancellationToken.None);
        var consumed = Assert.Single(consumedRows, row => row.Reason == TimeCreditReason.RotationSkipped);
        Assert.Equal(-20, consumed.Minutes);
        Assert.Equal(occurrence.Id, consumed.OccurrenceId);
    }

    [Fact]
    public async Task Credit_consumption_across_a_batch_uses_the_running_balance_not_the_starting_one()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(60));
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Uniform(60), Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        // 40 minutes of credit. The 1st occurrence spends 20 of it (Bjorn picked instead of
        // Anna); by the 2nd, Anna's remaining credit (20) exactly cancels Bjorn's new 20
        // minutes of real load, so the tie lands back on Anna - proving the running balance
        // updated in place rather than staying at its starting snapshot for the whole batch.
        // The 3rd sees the ratios diverge again (Bjorn still lower) and spends the last 20.
        _credits.Seed(MemberTimeCredit.Earned(household.Id, anna.Id, Monday, TimeCreditReason.WorkedAhead, 40, Guid.NewGuid()));

        for (var i = 0; i < 3; i++)
        {
            var definition = TaskDefinition.Create(household.Id, $"Uppgift {i}", 20, Now);
            definition.SetRecurrence(RecurrenceRule.Daily(Monday));
            definition.SetRotatingResponsibility(true);
            _definitions.Seed(definition);
        }

        await CreateUseCase().HandleAsync(household.Id, Monday, CancellationToken.None);

        var consumedRows = (await _credits.ListForMemberAsync(household.Id, anna.Id, Monday, Monday, CancellationToken.None))
            .Where(row => row.Reason == TimeCreditReason.RotationSkipped)
            .ToList();

        // Exactly two skips (40 minutes' worth) - the third occurrence found Anna's credit
        // exhausted and landed back on her by ratio, same as if she had never had any.
        Assert.Equal(2, consumedRows.Count);
        Assert.Equal(-40, consumedRows.Sum(row => row.Minutes));

        var totals = await _assignments.GetAssignedMinutesByMemberAsync(household.Id, CancellationToken.None);
        Assert.Equal(20, totals[anna.Id]);
        Assert.Equal(40, totals[bjorn.Id]);
    }
}
