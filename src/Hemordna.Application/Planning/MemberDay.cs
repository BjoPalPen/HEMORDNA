namespace Hemordna.Application.Planning;

/// <summary>
/// One member's whole day: the plan for what is left, plus what they already finished.
/// </summary>
/// <remarks>
/// <see cref="DailyPlanner"/> stays a pure function over outstanding work; completed tasks
/// are composed on top here rather than pushed into the planner, which has no business
/// knowing about them.
/// </remarks>
public sealed record MemberDay(DailyPlan Plan, IReadOnlyList<PlanCandidate> Completed)
{
    /// <summary>Minutes' worth of work already done today.</summary>
    public int CompletedMinutes => Completed.Sum(candidate => candidate.EstimatedMinutes);

    /// <summary>Everything on the day: what is done plus what is planned.</summary>
    public int TotalTaskCount => Completed.Count + Plan.Items.Count;

    /// <summary>True when there is nothing left to do and something was actually done.</summary>
    public bool IsDayComplete => Plan.Items.Count == 0 && Completed.Count > 0;
}
