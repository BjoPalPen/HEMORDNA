namespace Hemordna.Application.Planning;

/// <summary>
/// Builds one member's plan for one day: which outstanding tasks fit in the time they have,
/// in which order, and what is left over.
/// </summary>
/// <remarks>
/// <para>
/// The planner is a pure, deterministic function of its <see cref="DailyPlanRequest"/>. It
/// has no dependencies, reads no clock and touches no storage: the same request always
/// produces the same plan, regardless of the order the candidates arrive in.
/// </para>
/// <para>
/// The algorithm is deliberately simple - sort, then greedy first-fit. It is not an
/// optimiser and does not try to pack the day perfectly. It walks the ordered list and takes
/// every task that still fits in the remaining time, so a long task that does not fit does
/// not block the shorter ones behind it.
/// </para>
/// <para>
/// <b>Ordering rules</b>, applied in this order:
/// <list type="number">
///   <item>Tasks that cannot be deferred come first. They cannot be moved to another day at
///   all, so if they lose the budget they are simply lost.</item>
///   <item>Overdue tasks before tasks first due today. Something already late should not keep
///   slipping.</item>
///   <item>Higher priority before lower.</item>
///   <item>Earlier original due date first - the oldest work leads.</item>
///   <item>Shorter tasks first. At equal standing, finishing something beats starting
///   something, and it fits more of the day's work into the budget.</item>
///   <item>Occurrence id, ascending. A stable final tie-break so the ordering is total and
///   never depends on input order.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class DailyPlanner
{
    /// <summary>Produces the plan for the member, date and budget in <paramref name="request"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Available minutes are negative.</exception>
    public DailyPlan Plan(DailyPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Candidates);

        if (request.AvailableMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.AvailableMinutes,
                "Available minutes must not be negative.");
        }

        var date = request.Date;

        var ordered = request.Candidates
            .Where(candidate => IsEligible(candidate, date))
            .OrderBy(candidate => candidate.CanBeDeferred)
            .ThenByDescending(candidate => candidate.Occurrence.IsOverdueOn(date))
            .ThenByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Occurrence.OriginalScheduledDate)
            .ThenBy(candidate => candidate.EstimatedMinutes)
            .ThenBy(candidate => candidate.Occurrence.Id)
            .ToList();

        var items = new List<PlannedTask>(ordered.Count);
        var unplanned = new List<UnplannedTask>();
        var remainingMinutes = request.AvailableMinutes;

        foreach (var candidate in ordered)
        {
            if (candidate.EstimatedMinutes <= remainingMinutes)
            {
                items.Add(new PlannedTask(candidate, candidate.Occurrence.IsOverdueOn(date)));
                remainingMinutes -= candidate.EstimatedMinutes;
                continue;
            }

            var reason = request.AvailableMinutes == 0
                ? UnplannedReason.NoTimeAvailable
                : UnplannedReason.ExceedsRemainingTime;

            unplanned.Add(new UnplannedTask(candidate, reason));
        }

        return new DailyPlan(request.MemberId, date, request.AvailableMinutes, items, unplanned);
    }

    /// <summary>
    /// A candidate counts for today only if it still needs doing and is not scheduled for a
    /// later date. Anything else is not "left out for lack of time" - it is simply not part
    /// of this day - so it appears in neither list.
    /// </summary>
    private static bool IsEligible(PlanCandidate candidate, DateOnly date)
        => candidate.Occurrence.IsOutstanding && candidate.Occurrence.ScheduledDate <= date;
}
