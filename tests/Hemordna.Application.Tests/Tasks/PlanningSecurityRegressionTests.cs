using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Planning;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class PlanningSecurityRegressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 2);
    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly InMemoryMemberTimeCreditRepository _credits = new();
    private readonly SpyHouseholdNotifier _notifier = new();
    private readonly FixedTimeProvider _clock = new(Now);
    private EnsureOccurrencesGenerated Generator()
        => new(_households, _definitions, _occurrences, _assignments, _daysOff, _credits, _clock);
    private async Task<Household> HouseholdAsync()
        => await new CreateHousehold(_households, _clock).HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

    [Theory]
    [InlineData(1441)]
    [InlineData(int.MaxValue)]
    public async Task Extreme_extra_task_duration_is_rejected_without_persistence(int minutes)
    {
        var household = await HouseholdAsync();
        var useCase = new CreateExtraTask(_households, _definitions, _occurrences, _notifier, _clock);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.HandleAsync(
            household.Id, household.Members.Single().Id, new NewExtraTask("Diska", minutes), Today.AddDays(1), CancellationToken.None));
        Assert.Empty(await _definitions.ListByHouseholdAsync(household.Id, CancellationToken.None));
        Assert.Equal(0, _occurrences.AddCallCount);
        Assert.False(_notifier.WasNotified(household.Id));
    }

    [Fact]
    public async Task Extreme_schedule_date_is_rejected_before_assignment_or_occurrence_is_saved()
    {
        var household = await HouseholdAsync();
        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRotatingResponsibility(true);
        _definitions.Seed(definition);
        var useCase = new ScheduleTaskOccurrence(_households, _definitions, _occurrences, _assignments, _daysOff, _notifier, _clock);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.HandleAsync(
            household.Id, definition.Id, DateOnly.MaxValue, household.Members.Single().Id, CancellationToken.None));
        Assert.Equal(0, _assignments.Count);
        Assert.Equal(0, _occurrences.AddCallCount);
        Assert.False(_notifier.WasNotified(household.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Future_bookings_do_not_hide_due_recurrence_and_repeated_generation_is_idempotent(bool legacyMaximum)
    {
        var household = await HouseholdAsync();
        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Today));
        _definitions.Seed(definition);
        var future = definition.ScheduleFor(Today.AddDays(2), Now);
        if (legacyMaximum)
        {
            // Simulate EF hydration of state persisted before date validation existed.
            typeof(TaskOccurrence).GetProperty(nameof(TaskOccurrence.OriginalScheduledDate))!.SetValue(future, DateOnly.MaxValue);
            typeof(TaskOccurrence).GetProperty(nameof(TaskOccurrence.ScheduledDate))!.SetValue(future, DateOnly.MaxValue);
        }
        _occurrences.Seed(future);
        await Generator().HandleAsync(household.Id, Today, CancellationToken.None);
        await Generator().HandleAsync(household.Id, Today, CancellationToken.None);
        Assert.Equal(1, _occurrences.AddCallCount);
        Assert.Equal(Today, await _occurrences.FindMostRecentOriginalDateAsync(household.Id, definition.Id, Today, CancellationToken.None));
        Assert.Equal(legacyMaximum ? DateOnly.MaxValue : Today.AddDays(2), future.OriginalScheduledDate);
        if (!legacyMaximum)
        {
            await Generator().HandleAsync(household.Id, Today.AddDays(2), CancellationToken.None);
            Assert.Equal(2, _occurrences.AddCallCount); // Only the intervening day is new.
        }
    }

    [Fact]
    public async Task Legacy_large_credits_and_maximum_date_do_not_block_another_members_daily_plan()
    {
        var household = await HouseholdAsync();
        var attacker = household.Members.Single();
        var other = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now);
        for (var i = 0; i < 2; i++)
            _credits.Seed(MemberTimeCredit.Earned(household.Id, attacker.Id, Today, TimeCreditReason.WorkedAhead, int.MaxValue, Guid.NewGuid()));
        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Today));
        _definitions.Seed(definition);
        var legacy = definition.ScheduleFor(Today, Now);
        typeof(TaskOccurrence).GetProperty(nameof(TaskOccurrence.OriginalScheduledDate))!.SetValue(legacy, DateOnly.MaxValue);
        typeof(TaskOccurrence).GetProperty(nameof(TaskOccurrence.ScheduledDate))!.SetValue(legacy, DateOnly.MaxValue);
        _occurrences.Seed(legacy);
        var plans = new GetDailyPlan(_households, new InMemoryMemberAvailabilityRepository(), _daysOff,
            new InMemoryPlanCandidateQuery(), Generator(), new DailyPlanner());
        var day = await plans.HandleAsync(household.Id, other.Id, Today, CancellationToken.None);
        Assert.NotNull(day);
        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Extreme_extra_task_date_does_not_leave_a_definition_behind()
    {
        var household = await HouseholdAsync();
        var useCase = new CreateExtraTask(_households, _definitions, _occurrences, _notifier, _clock);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useCase.HandleAsync(
            household.Id, household.Members.Single().Id, new NewExtraTask("Diska", 20), DateOnly.MaxValue, CancellationToken.None));
        Assert.Empty(await _definitions.ListByHouseholdAsync(household.Id, CancellationToken.None));
    }
    [Fact]
    public async Task Large_future_catch_up_continues_past_already_covered_slots()
    {
        var household = await HouseholdAsync();
        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Today));
        _definitions.Seed(definition);
        for (var i = 0; i < 3; i++)
            await Generator().HandleAsync(household.Id, Today.AddDays(400), CancellationToken.None);
        Assert.Equal(401, _occurrences.AddCallCount);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completed_or_skipped_future_bookings_are_not_generated_again(bool completed)
    {
        var household = await HouseholdAsync();
        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Today));
        _definitions.Seed(definition);
        var future = definition.ScheduleFor(Today.AddDays(2), Now);
        if (completed)
            future.Complete(household.Members.Single().Id, Now);
        else
            future.Skip();
        _occurrences.Seed(future);
        await Generator().HandleAsync(household.Id, Today.AddDays(2), CancellationToken.None);
        await Generator().HandleAsync(household.Id, Today.AddDays(2), CancellationToken.None);
        Assert.Equal(2, _occurrences.AddCallCount);
    }
}