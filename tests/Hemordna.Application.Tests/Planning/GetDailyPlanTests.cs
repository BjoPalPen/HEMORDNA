using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

public class GetDailyPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    // 2026-02-06 is a Friday.
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberAvailabilityRepository _availabilities = new();
    private readonly InMemoryPlanCandidateQuery _candidates = new();

    private GetDailyPlan CreateUseCase()
        => new(_households, _availabilities, _candidates, new DailyPlanner());

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync(int fridayMinutes)
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var member = household.Members.Single();
        member.ChangeWeeklyTimeBudget(
            WeeklyTimeBudget.Empty.WithDay(DayOfWeek.Friday, fridayMinutes));

        return (household.Id, member);
    }

    private void GiveMemberTask(Guid householdId, Guid memberId, string name, int minutes)
    {
        var definition = TaskDefinition.Create(householdId, name, minutes, Now);
        var occurrence = definition.ScheduleFor(Friday, Now);
        occurrence.AssignTo(memberId);

        _candidates.AssignToMember(memberId, new PlanCandidate(occurrence, name));
    }

    [Fact]
    public async Task Plans_the_members_day_within_their_weekly_budget()
    {
        var (householdId, member) = await ArrangeHouseholdAsync(fridayMinutes: 30);
        GiveMemberTask(householdId, member.Id, "Hall", 7);
        GiveMemberTask(householdId, member.Id, "Dammsug vardagsrum", 10);
        GiveMemberTask(householdId, member.Id, "Matrum", 5);

        var day = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        var plan = day.Plan;
        Assert.Equal(30, plan.AvailableMinutes);
        Assert.Equal(22, plan.PlannedMinutes);
        Assert.Equal(3, plan.Items.Count);
        Assert.Empty(plan.Unplanned);
    }

    [Fact]
    public async Task A_one_off_override_shrinks_the_day_without_touching_the_week()
    {
        var (householdId, member) = await ArrangeHouseholdAsync(fridayMinutes: 30);
        GiveMemberTask(householdId, member.Id, "Hall", 7);
        GiveMemberTask(householdId, member.Id, "Dammsug vardagsrum", 10);

        await new SetMemberAvailability(_households, _availabilities)
            .HandleAsync(householdId, member.Id, Friday, 8, CancellationToken.None);

        var day = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        var plan = day.Plan;
        Assert.Equal(8, plan.AvailableMinutes);
        Assert.Equal("Hall", Assert.Single(plan.Items).Candidate.TaskName);
        Assert.Equal("Dammsug vardagsrum", Assert.Single(plan.Unplanned).Candidate.TaskName);

        // Next Friday is back to normal.
        Assert.Equal(30, member.WeeklyTimeBudget.MinutesFor(DayOfWeek.Friday));
    }

    [Fact]
    public async Task No_time_today_yields_an_empty_plan_rather_than_an_error()
    {
        var (householdId, member) = await ArrangeHouseholdAsync(fridayMinutes: 0);
        GiveMemberTask(householdId, member.Id, "Hall", 7);

        var day = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        var plan = day.Plan;
        Assert.True(plan.IsEmpty);
        Assert.Equal(UnplannedReason.NoTimeAvailable, Assert.Single(plan.Unplanned).Reason);
    }

    [Fact]
    public async Task A_member_with_nothing_to_do_gets_an_empty_plan()
    {
        var (householdId, member) = await ArrangeHouseholdAsync(fridayMinutes: 30);

        var day = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        var plan = day.Plan;
        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Unplanned);
    }

    [Fact]
    public async Task Another_members_work_never_appears_in_this_members_day()
    {
        var (householdId, member) = await ArrangeHouseholdAsync(fridayMinutes: 60);
        var otherMemberId = Guid.NewGuid();

        GiveMemberTask(householdId, member.Id, "Mitt", 10);
        GiveMemberTask(householdId, otherMemberId, "Någon annans", 10);

        var day = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        var plan = day.Plan;
        Assert.Equal("Mitt", Assert.Single(plan.Items).Candidate.TaskName);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync(fridayMinutes: 30);

        var day = await CreateUseCase()
            .HandleAsync(householdId, Guid.NewGuid(), Friday, CancellationToken.None);

        Assert.Null(day);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var day = await CreateUseCase()
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Friday, CancellationToken.None);

        Assert.Null(day);
    }
}

public class MemberDayCompletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberAvailabilityRepository _availabilities = new();
    private readonly InMemoryPlanCandidateQuery _candidates = new();

    private GetDailyPlan CreateUseCase()
        => new(_households, _availabilities, _candidates, new DailyPlanner());

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var member = household.Members.Single();
        member.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Empty.WithDay(DayOfWeek.Friday, 30));

        return (household.Id, member);
    }

    private TaskOccurrence GiveTask(Guid householdId, Guid memberId, string name, int minutes)
    {
        var occurrence = TaskDefinition.Create(householdId, name, minutes, Now).ScheduleFor(Friday, Now);
        occurrence.AssignTo(memberId);
        _candidates.AssignToMember(memberId, new PlanCandidate(occurrence, name));
        return occurrence;
    }

    [Fact]
    public async Task Finished_work_is_reported_alongside_what_is_left()
    {
        var (householdId, member) = await ArrangeAsync();
        var done = GiveTask(householdId, member.Id, "Torka av köksbänkar", 5);
        GiveTask(householdId, member.Id, "Dammsug vardagsrum", 10);
        done.Complete(member.Id, Now);

        var day = await CreateUseCase().HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        Assert.Equal("Torka av köksbänkar", Assert.Single(day.Completed).TaskName);
        Assert.Equal(5, day.CompletedMinutes);
        Assert.Equal("Dammsug vardagsrum", Assert.Single(day.Plan.Items).Candidate.TaskName);
        Assert.Equal(2, day.TotalTaskCount);
        Assert.False(day.IsDayComplete);
    }

    [Fact]
    public async Task A_finished_task_no_longer_takes_room_in_the_budget()
    {
        var (householdId, member) = await ArrangeAsync();
        var done = GiveTask(householdId, member.Id, "Klar", 25);
        GiveTask(householdId, member.Id, "Kvar", 20);
        done.Complete(member.Id, Now);

        var day = await CreateUseCase().HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        // Without completion the 20-minute task would not have fitted beside the 25-minute one.
        Assert.NotNull(day);
        Assert.Equal("Kvar", Assert.Single(day.Plan.Items).Candidate.TaskName);
        Assert.Empty(day.Plan.Unplanned);
    }

    [Fact]
    public async Task The_day_is_complete_when_everything_is_done()
    {
        var (householdId, member) = await ArrangeAsync();
        var done = GiveTask(householdId, member.Id, "Hall", 7);
        done.Complete(member.Id, Now);

        var day = await CreateUseCase().HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        Assert.True(day.IsDayComplete);
    }

    [Fact]
    public async Task An_empty_day_is_not_a_completed_day()
    {
        // Nothing done and nothing to do should not be celebrated as an achievement.
        var (householdId, member) = await ArrangeAsync();

        var day = await CreateUseCase().HandleAsync(householdId, member.Id, Friday, CancellationToken.None);

        Assert.NotNull(day);
        Assert.False(day.IsDayComplete);
    }
}
