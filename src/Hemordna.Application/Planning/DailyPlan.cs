namespace Hemordna.Application.Planning;

/// <summary>Why a candidate did not make it into today's plan.</summary>
public enum UnplannedReason
{
    /// <summary>The member has no time at all today.</summary>
    NoTimeAvailable = 0,

    /// <summary>The task is longer than the time left after the tasks above it.</summary>
    ExceedsRemainingTime = 1
}

/// <summary>A task that fits in today's plan, in the order it is meant to be done.</summary>
/// <param name="Candidate">The task instance.</param>
/// <param name="IsOverdue">Whether it was first due before today.</param>
public sealed record PlannedTask(PlanCandidate Candidate, bool IsOverdue);

/// <summary>
/// A task that did not fit today. Note that a task that cannot be deferred can still end up
/// here - the planner cannot create time - and a caller may want to surface those
/// differently by filtering on <c>Candidate.CanBeDeferred</c>.
/// </summary>
/// <param name="Candidate">The task instance.</param>
/// <param name="Reason">Why it was left out.</param>
public sealed record UnplannedTask(PlanCandidate Candidate, UnplannedReason Reason);

/// <summary>
/// One member's day: what to do, in what order, and what was left out.
/// </summary>
public sealed record DailyPlan(
    Guid MemberId,
    DateOnly Date,
    int AvailableMinutes,
    IReadOnlyList<PlannedTask> Items,
    IReadOnlyList<UnplannedTask> Unplanned)
{
    /// <summary>Total estimated minutes of everything in <see cref="Items"/>.</summary>
    public int PlannedMinutes => Items.Sum(item => item.Candidate.EstimatedMinutes);

    /// <summary>Minutes of the budget still unused.</summary>
    public int RemainingMinutes => AvailableMinutes - PlannedMinutes;

    /// <summary>True when there is nothing to do today.</summary>
    public bool IsEmpty => Items.Count == 0;
}
