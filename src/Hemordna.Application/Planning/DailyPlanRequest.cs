namespace Hemordna.Application.Planning;

/// <summary>
/// Everything the planner needs for one member on one day. The date and the available
/// minutes are passed in explicitly - the planner never reads a clock, which is what makes
/// it deterministic and testable.
/// </summary>
/// <param name="MemberId">The member the plan is for.</param>
/// <param name="Date">The day being planned.</param>
/// <param name="AvailableMinutes">
/// Minutes this member has today: the one-off override for the date if one exists,
/// otherwise the normal weekly budget. Zero is valid and means "no time today".
/// </param>
/// <param name="Candidates">
/// Outstanding task instances that could be done. Candidates that are already completed or
/// skipped, or that are scheduled for a later date, are ignored by the planner.
/// </param>
public sealed record DailyPlanRequest(
    Guid MemberId,
    DateOnly Date,
    int AvailableMinutes,
    IReadOnlyCollection<PlanCandidate> Candidates);
