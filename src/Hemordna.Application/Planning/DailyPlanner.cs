using Hemordna.Domain.Tasks;

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
/// not block the shorter ones behind it. The one exception is a task brought forward from
/// tomorrow (<see cref="Hemordna.Domain.Tasks.TaskOccurrence.IsBroughtForwardOn"/>): choosing
/// to do it today already happened elsewhere, so it always lands in <c>Items</c>, even past the
/// budget - see docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i
/// förväg".
/// </para>
/// <para>
/// <b>Effort ceiling ("Hur är orken idag?" - "Lite"), applied before ordering.</b> A non-routine
/// candidate heavier than <see cref="DailyPlanRequest.EffortCeiling"/> is diverted straight to
/// <c>Unplanned</c> (<see cref="UnplannedReason.ExceedsEffortToday"/>) and never enters the
/// picking loop below at all - see <see cref="ExceedsEffortCeiling"/> and docs/ARCHITECTURE.md
/// "Beslut: orkvalet styr dagens tyngd".
/// </para>
/// <para>
/// <b>Ordering rules</b>, applied in this order:
/// <list type="number">
///   <item><b>Routines first, unconditionally</b> (Björns beslut: "överst och alltid med" - see
///   <see cref="PlanCandidate.IsRoutine"/>/<see cref="Hemordna.Domain.Tasks.VisitKindClassifier"/>).
///   A quick daily routine - making the bed, airing out a room - must never lose its place to a
///   heavier task that merely outranks it on priority or due date. This is a deliberate
///   trade-off: a task that cannot be deferred at all (rule 2) now comes AFTER every routine,
///   even though losing the budget loses that task outright, because a routine recurs every
///   single day regardless and "first and always" was the explicit, stronger requirement.</item>
///   <item>Tasks that cannot be deferred come first among the rest. They cannot be moved to
///   another day at all, so if they lose the budget they are simply lost.</item>
///   <item>Overdue tasks before tasks first due today. Something already late should not keep
///   slipping.</item>
///   <item>Higher priority before lower.</item>
///   <item>Earlier original due date first - the oldest work leads.</item>
///   <item>A task sharing a room, or a floor (see <see cref="TaskCluster"/>), with something
///   already chosen for this same day-so-far - people naturally finish a room, or a floor,
///   before moving to the next rather than hopping between them. Only a soft preference among
///   candidates already tied on everything above: it never promotes a task ahead of something
///   more overdue or higher-priority, and the very first pick of the day is unaffected (nothing
///   is chosen yet to share a room with).</item>
///   <item>Shorter tasks first. At equal standing, finishing something beats starting
///   something, and it fits more of the day's work into the budget.</item>
///   <item>A handful of well-known "do X before Y" chore pairs (see
///   <see cref="ChoreSequenceHint"/>) - e.g. vacuum a floor before mopping it. Only ever a
///   nudge between two tasks already tied on everything above.</item>
///   <item>Occurrence id, ascending. A stable final tie-break so the ordering is total and
///   never depends on input order.</item>
/// </list>
/// Rule 6 (room/floor clustering) is the only one where a pick depends on picks already made
/// today, so unlike the rest this cannot be a single static sort: candidates are chosen one at a
/// time, in order, each pick re-evaluating which rooms/floors are already represented among
/// today's picks so far.
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

        var eligible = request.Candidates.Where(candidate => IsEligible(candidate, date)).ToList();
        var items = new List<PlannedTask>(eligible.Count);
        var unplanned = new List<UnplannedTask>();

        // "Orkvalet styr dagens tyngd" ("Lite"): a candidate heavier than today's ceiling never
        // enters the picking loop below - it cannot win the budget, cannot open a room cluster,
        // and is reported with its own reason rather than being silently dropped or mixed in
        // with "did not fit in time". A routine is always exempt - "överst och alltid med" - see
        // docs/ARCHITECTURE.md "Beslut: orkvalet styr dagens tyngd" for why only "Lite" ever sets
        // a ceiling at all (Lagom/Mycket leave it null): the person's usual capacity per weekday
        // already gates HEAVY work at assignment time (RotationPicker.EligibleMembers), so
        // filtering it again here would strand whatever the rotation fell back to them for.
        var remaining = new List<PlanCandidate>(eligible.Count);

        foreach (var candidate in eligible)
        {
            if (ExceedsEffortCeiling(candidate, request.EffortCeiling))
            {
                unplanned.Add(new UnplannedTask(candidate, UnplannedReason.ExceedsEffortToday));
                continue;
            }

            remaining.Add(candidate);
        }
        // What is already ticked off today has used today's time. Without this, every
        // completion freed time, and since Idag re-fetches the plan after each one the planner
        // refilled the day: "4 kvar", two ticked off, "5 kvar" - the day never ended. See
        // docs/ARCHITECTURE.md "Beslut: avbockat räknas av dagens tid".
        var remainingMinutes = Math.Max(0, request.AvailableMinutes - request.CompletedMinutes);
        var chosenClusters = new HashSet<string>();

        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderByDescending(candidate => candidate.IsRoutine)
                .ThenBy(candidate => candidate.CanBeDeferred)
                .ThenByDescending(candidate => candidate.Occurrence.IsOverdueOn(date))
                .ThenByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.Occurrence.OriginalScheduledDate)
                .ThenByDescending(candidate => IsInAnAlreadyChosenCluster(candidate, chosenClusters))
                .ThenBy(candidate => candidate.EstimatedMinutes)
                .ThenBy(candidate => ChoreSequenceHint.RankFor(candidate.TaskName))
                .ThenBy(candidate => candidate.Occurrence.Id)
                .First();

            remaining.Remove(next);

            // A task brought forward from tomorrow (see TaskOccurrence.BringForwardTo) was
            // already a deliberate choice to do it today - the budget cannot un-choose it, so it
            // is never bumped to Unplanned for lack of room, even when it pushes the day over.
            if (next.EstimatedMinutes <= remainingMinutes || next.Occurrence.IsBroughtForwardOn(date))
            {
                items.Add(new PlannedTask(next, next.Occurrence.IsOverdueOn(date)));
                remainingMinutes -= next.EstimatedMinutes;

                if (TaskCluster.KeyFor(next.AreaName, next.Floor) is { } chosenKey)
                {
                    chosenClusters.Add(chosenKey);
                }

                continue;
            }

            var reason = request.AvailableMinutes == 0
                ? UnplannedReason.NoTimeAvailable
                : UnplannedReason.ExceedsRemainingTime;

            unplanned.Add(new UnplannedTask(next, reason));
        }

        return new DailyPlan(request.MemberId, date, request.AvailableMinutes, items, unplanned, request.CompletedMinutes);
    }

    private static bool IsInAnAlreadyChosenCluster(PlanCandidate candidate, HashSet<string> chosenClusters)
        => TaskCluster.KeyFor(candidate.AreaName, candidate.Floor) is { } key && chosenClusters.Contains(key);

    /// <summary>True when today's ceiling rules this candidate out - never for a routine
    /// ("överst och alltid med" outranks the ceiling too, same trade-off as rule 1's own
    /// remarks), and never when there is no ceiling at all (<paramref name="ceiling"/> is
    /// <c>null</c> - "Lagom"/"Mycket", or no choice made yet).</summary>
    private static bool ExceedsEffortCeiling(PlanCandidate candidate, TaskEffort? ceiling)
        => ceiling is { } max && !candidate.IsRoutine && candidate.Effort > max;

    /// <summary>
    /// A candidate counts for today only if it still needs doing and is not scheduled for a
    /// later date. Anything else is not "left out for lack of time" - it is simply not part
    /// of this day - so it appears in neither list.
    /// </summary>
    private static bool IsEligible(PlanCandidate candidate, DateOnly date)
        => candidate.Occurrence.IsOutstanding && candidate.Occurrence.ScheduledDate <= date;
}
