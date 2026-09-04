using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

public class DailyPlannerTests
{
    /// <summary>2026-02-06. Every test plans this exact day - the planner never reads a clock.</summary>
    internal static readonly DateOnly Friday = new(2026, 2, 6);

    private static readonly Guid AnnaId = Guid.NewGuid();

    private readonly DailyPlanner _planner = new();

    private DailyPlan PlanWith(int availableMinutes, params PlanCandidate[] candidates)
        => _planner.Plan(new DailyPlanRequest(AnnaId, Friday, availableMinutes, candidates));

    private static string[] NamesOf(DailyPlan plan)
        => plan.Items.Select(item => item.Candidate.TaskName).ToArray();

    [Fact]
    public void A_member_with_no_tasks_gets_an_empty_plan()
    {
        var plan = PlanWith(30);

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Unplanned);
        Assert.Equal(0, plan.PlannedMinutes);
        Assert.Equal(30, plan.RemainingMinutes);
        Assert.Equal(AnnaId, plan.MemberId);
        Assert.Equal(Friday, plan.Date);
    }

    [Fact]
    public void Everything_that_fits_within_the_budget_is_planned()
    {
        var plan = PlanWith(
            30,
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build(),
            PlanCandidateBuilder.Task("Dammsug vardagsrum").Minutes(10).Build(),
            PlanCandidateBuilder.Task("Matrum").Minutes(5).Build(),
            PlanCandidateBuilder.Task("Hundhar i soffan").Minutes(3).Build());

        Assert.Equal(4, plan.Items.Count);
        Assert.Empty(plan.Unplanned);
        Assert.Equal(25, plan.PlannedMinutes);
        Assert.Equal(5, plan.RemainingMinutes);
    }

    [Fact]
    public void The_budget_is_never_exceeded()
    {
        var plan = PlanWith(
            20,
            PlanCandidateBuilder.Task("A").Minutes(15).Build(),
            PlanCandidateBuilder.Task("B").Minutes(15).Build());

        Assert.Single(plan.Items);
        Assert.Equal(15, plan.PlannedMinutes);
        Assert.True(plan.PlannedMinutes <= plan.AvailableMinutes);
        Assert.Equal(UnplannedReason.ExceedsRemainingTime, Assert.Single(plan.Unplanned).Reason);
    }

    [Fact]
    public void With_no_time_available_nothing_is_planned()
    {
        var plan = PlanWith(
            0,
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build(),
            PlanCandidateBuilder.Task("Matrum").Minutes(5).Build());

        Assert.True(plan.IsEmpty);
        Assert.Equal(2, plan.Unplanned.Count);
        Assert.All(plan.Unplanned, task => Assert.Equal(UnplannedReason.NoTimeAvailable, task.Reason));
    }

    [Fact]
    public void A_task_longer_than_the_whole_day_does_not_block_the_shorter_ones()
    {
        var plan = PlanWith(
            30,
            PlanCandidateBuilder.Task("Storstada").Minutes(120).Build(),
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build(),
            PlanCandidateBuilder.Task("Matrum").Minutes(5).Build());

        Assert.Equal(["Matrum", "Hall"], NamesOf(plan));
        Assert.Equal("Storstada", Assert.Single(plan.Unplanned).Candidate.TaskName);
        Assert.Equal(UnplannedReason.ExceedsRemainingTime, plan.Unplanned[0].Reason);
    }

    [Fact]
    public void Higher_priority_is_planned_first()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Lag").Minutes(10).Priority(TaskPriority.Low).Build(),
            PlanCandidateBuilder.Task("Hog").Minutes(10).Priority(TaskPriority.High).Build(),
            PlanCandidateBuilder.Task("Normal").Minutes(10).Priority(TaskPriority.Normal).Build());

        Assert.Equal(["Hog", "Normal", "Lag"], NamesOf(plan));
    }

    [Fact]
    public void Higher_priority_wins_the_budget_when_time_is_short()
    {
        var plan = PlanWith(
            10,
            PlanCandidateBuilder.Task("Lag").Minutes(10).Priority(TaskPriority.Low).Build(),
            PlanCandidateBuilder.Task("Hog").Minutes(10).Priority(TaskPriority.High).Build());

        Assert.Equal("Hog", Assert.Single(plan.Items).Candidate.TaskName);
        Assert.Equal("Lag", Assert.Single(plan.Unplanned).Candidate.TaskName);
    }

    [Fact]
    public void Overdue_tasks_are_planned_before_tasks_first_due_today()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Idag").Minutes(10).Build(),
            PlanCandidateBuilder.Task("Forfallen").Minutes(10).DueDaysAgo(3).Build());

        Assert.Equal(["Forfallen", "Idag"], NamesOf(plan));
        Assert.True(plan.Items[0].IsOverdue);
        Assert.False(plan.Items[1].IsOverdue);
    }

    [Fact]
    public void Among_overdue_tasks_the_oldest_comes_first()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Tva dagar").Minutes(10).DueDaysAgo(2).Build(),
            PlanCandidateBuilder.Task("Fem dagar").Minutes(10).DueDaysAgo(5).Build(),
            PlanCandidateBuilder.Task("En dag").Minutes(10).DueDaysAgo(1).Build());

        Assert.Equal(["Fem dagar", "Tva dagar", "En dag"], NamesOf(plan));
    }

    [Fact]
    public void An_overdue_task_outranks_a_higher_priority_task_due_today()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Hog idag").Minutes(10).Priority(TaskPriority.High).Build(),
            PlanCandidateBuilder.Task("Lag forfallen").Minutes(10).Priority(TaskPriority.Low).DueDaysAgo(1).Build());

        Assert.Equal(["Lag forfallen", "Hog idag"], NamesOf(plan));
    }

    [Fact]
    public void Tasks_that_cannot_be_deferred_are_planned_first()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Kan skjutas upp").Minutes(10).Priority(TaskPriority.High).Build(),
            PlanCandidateBuilder.Task("Maste goras idag").Minutes(10).Priority(TaskPriority.Low).NotDeferrable().Build());

        // A task that cannot move to another day is lost if it does not happen today, so it
        // takes the budget ahead of anything that can wait - overdue or high priority included.
        Assert.Equal(["Maste goras idag", "Kan skjutas upp"], NamesOf(plan));
    }

    [Fact]
    public void A_non_deferrable_task_outranks_an_overdue_deferrable_one()
    {
        var plan = PlanWith(
            10,
            PlanCandidateBuilder.Task("Forfallen").Minutes(10).DueDaysAgo(4).Build(),
            PlanCandidateBuilder.Task("Maste goras idag").Minutes(10).NotDeferrable().Build());

        Assert.Equal("Maste goras idag", Assert.Single(plan.Items).Candidate.TaskName);
    }

    [Fact]
    public void A_non_deferrable_task_that_does_not_fit_is_still_reported()
    {
        // The planner cannot create time. It must not silently drop work that cannot wait.
        var plan = PlanWith(
            30,
            PlanCandidateBuilder.Task("Lang och maste goras idag").Minutes(90).NotDeferrable().Build(),
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build());

        var unplanned = Assert.Single(plan.Unplanned);
        Assert.Equal("Lang och maste goras idag", unplanned.Candidate.TaskName);
        Assert.False(unplanned.Candidate.CanBeDeferred);
        Assert.Equal(["Hall"], NamesOf(plan));
    }

    [Fact]
    public void At_equal_standing_the_shorter_task_comes_first()
    {
        var plan = PlanWith(
            60,
            PlanCandidateBuilder.Task("Lang").Minutes(20).Build(),
            PlanCandidateBuilder.Task("Kort").Minutes(3).Build(),
            PlanCandidateBuilder.Task("Mellan").Minutes(10).Build());

        Assert.Equal(["Kort", "Mellan", "Lang"], NamesOf(plan));
    }

    [Fact]
    public void Tasks_that_are_identical_on_every_rule_are_still_ordered_deterministically()
    {
        var candidates = Enumerable.Range(0, 6)
            .Select(_ => PlanCandidateBuilder.Task("Samma").Minutes(5).Build())
            .ToArray();

        var first = PlanWith(60, candidates);
        var reversed = PlanWith(60, candidates.Reverse().ToArray());

        Assert.Equal(6, first.Items.Count);
        Assert.Equal(
            first.Items.Select(item => item.Candidate.Occurrence.Id),
            reversed.Items.Select(item => item.Candidate.Occurrence.Id));
    }

    [Fact]
    public void The_plan_does_not_depend_on_the_order_the_candidates_arrive_in()
    {
        var candidates = new[]
        {
            PlanCandidateBuilder.Task("A").Minutes(10).Priority(TaskPriority.High).Build(),
            PlanCandidateBuilder.Task("B").Minutes(5).DueDaysAgo(2).Build(),
            PlanCandidateBuilder.Task("C").Minutes(15).NotDeferrable().Build(),
            PlanCandidateBuilder.Task("D").Minutes(5).Priority(TaskPriority.Low).Build(),
            PlanCandidateBuilder.Task("E").Minutes(40).Build()
        };

        var expected = NamesOf(PlanWith(30, candidates));

        // Every rotation of the same input must produce the same plan.
        for (var offset = 1; offset < candidates.Length; offset++)
        {
            var rotated = candidates.Skip(offset).Concat(candidates.Take(offset)).ToArray();

            Assert.Equal(expected, NamesOf(PlanWith(30, rotated)));
        }
    }

    [Fact]
    public void Completed_and_skipped_tasks_are_not_part_of_the_day()
    {
        var completed = PlanCandidateBuilder.Task("Redan klar").Minutes(10).BuildOccurrence();
        completed.Complete(AnnaId, new DateTimeOffset(2026, 2, 5, 19, 0, 0, TimeSpan.Zero));

        var skipped = PlanCandidateBuilder.Task("Behovdes inte").Minutes(10).BuildOccurrence();
        skipped.Skip();

        var plan = PlanWith(
            60,
            new PlanCandidate(completed, "Redan klar"),
            new PlanCandidate(skipped, "Behovdes inte"),
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build());

        Assert.Equal(["Hall"], NamesOf(plan));
        Assert.Empty(plan.Unplanned);
    }

    [Fact]
    public void A_task_deferred_to_a_later_date_leaves_todays_plan()
    {
        var deferred = PlanCandidateBuilder.Task("Uppskjuten").Minutes(10).BuildOccurrence();
        deferred.DeferTo(Friday.AddDays(2));

        var plan = PlanWith(60, new PlanCandidate(deferred, "Uppskjuten"));

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Unplanned);
    }

    [Fact]
    public void A_task_scheduled_for_a_future_date_is_not_planned_today()
    {
        var plan = PlanWith(60, PlanCandidateBuilder.Task("Nasta vecka").Minutes(10).On(Friday.AddDays(7)).Build());

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.Unplanned);
    }

    [Fact]
    public void A_reduced_budget_for_today_shrinks_the_plan_without_losing_the_tasks()
    {
        var candidates = new[]
        {
            PlanCandidateBuilder.Task("Hall").Minutes(7).Build(),
            PlanCandidateBuilder.Task("Dammsug vardagsrum").Minutes(10).Build(),
            PlanCandidateBuilder.Task("Matrum").Minutes(5).Build()
        };

        var normalDay = PlanWith(30, candidates);
        var shortDay = PlanWith(10, candidates);

        Assert.Equal(3, normalDay.Items.Count);
        Assert.Equal(["Matrum"], NamesOf(shortDay));
        Assert.Equal(2, shortDay.Unplanned.Count);
        Assert.All(shortDay.Unplanned, task => Assert.Equal(UnplannedReason.ExceedsRemainingTime, task.Reason));
    }

    [Fact]
    public void A_negative_budget_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PlanWith(-1));

    [Fact]
    public void A_candidate_needs_a_task_name()
    {
        var occurrence = PlanCandidateBuilder.Task("Hall").BuildOccurrence();

        Assert.Throws<ArgumentException>(() => new PlanCandidate(occurrence, "   "));
    }
}
