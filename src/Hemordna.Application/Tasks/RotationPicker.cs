using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Decides who is next for a rotating task: whoever is currently furthest below their fair
/// share of the household's rotating workload, relative to how much time they normally have.
/// </summary>
/// <remarks>
/// This replaces a simpler "whoever did it last, take the next person in join order" scheme,
/// which broke down in two ways a real household hits immediately:
/// <list type="bullet">
///   <item>
///   <b>Cold start.</b> A definition with no assignment history yet always fell back to
///   <c>eligible[0]</c> - the earliest-joined member. A household that creates many rotating
///   tasks in one sitting (the normal way to set up Områden) had every single one default to
///   the same person, because none of them had history yet - see docs/ARCHITECTURE.md §6.
///   </item>
///   <item>
///   <b>Equal turns, unequal time.</b> Strict alternation assumes every member can spare the
///   same amount of time, which is not true for a full-time worker next to a retiree or a
///   part-time member.
///   </item>
/// </list>
/// Weighing by <see cref="WeeklyTimeBudget.TotalWeeklyMinutes"/> fixes both: a member with no
/// history is simply the most under-served relative to their share (rather than a special
/// case), and a member with more normal free time is offered more of the rotating work,
/// proportionally.
/// </remarks>
/// <remarks>
/// <b>Daily cap (2026-09-07).</b> The ratio above is all-time cumulative (see
/// <see cref="EnsureOccurrencesGenerated"/>'s <c>assignedMinutesByMember</c>), which is exactly
/// right for deciding whose TURN it is next - but a household that had gone badly lopsided
/// before this scheme existed (or before a member was added, or came back from a long pause)
/// has one member sitting at a far lower ratio than everyone else for a while. Left unchecked,
/// EVERY pick in a batch that creates many rotating tasks at once (again, the normal way to set
/// up Områden) goes to that one under-served member until their ratio catches up - dumping an
/// entire backlog of the day's chores on whoever has the LEAST slack today, since ratio alone
/// says nothing about whether today is realistic for them. <see cref="PickNext"/> therefore
/// prefers a member who still has room in their OWN day
/// (<see cref="WeeklyTimeBudget.MinutesFor"/>) for this task, among those tied for lowest ratio
/// or not; only once nobody has room left today does it fall back to ratio alone, so the task
/// still gets a home rather than none at all - <see cref="Planning.DailyPlanner"/> is what
/// actually decides, per member, what fits versus what waits for another day.
/// </remarks>
internal static class RotationPicker
{
    /// <summary>
    /// Who got the task, and - when "tid i förväg" changed the outcome - whose credit paid for
    /// that. <see cref="ConsumedFromMemberId"/> is <c>null</c> (and <see cref="ConsumedMinutes"/>
    /// zero) whenever credit made no difference to the pick, which is the ordinary case.
    /// </summary>
    internal sealed record PickResult(Guid MemberId, Guid? ConsumedFromMemberId, int ConsumedMinutes);

    /// <summary>
    /// <paramref name="assignedMinutesByMember"/> must reflect every assignment already made in
    /// the current batch, not just what is in the database - see
    /// <see cref="EnsureOccurrencesGenerated"/>, which generates many occurrences in one pass
    /// and updates this in place after each pick precisely so the second pick in a batch does
    /// not repeat the cold-start mistake the first one would otherwise make.
    /// <paramref name="assignedMinutesTodayOnDate"/> is the same kind of running total, but
    /// scoped to <paramref name="today"/> alone - see the daily-cap remarks above.
    /// <paramref name="daysOff"/> is an outright exclusion (see <see cref="EligibleMembers"/>);
    /// <paramref name="creditMinutes"/> only ever biases the ratio among whoever is still
    /// eligible - see the "tid i förväg" remarks below.
    /// </summary>
    /// <remarks>
    /// <b>Tid i förväg (2026-09-08).</b> A member's outstanding credit
    /// (<c>MemberTimeCredit.BalanceOf</c>) is added to their assigned minutes before dividing by
    /// capacity, so someone who worked ahead LOOKS more loaded than their raw assignments alone
    /// would suggest - which is exactly the point: it nudges the next pick toward someone else,
    /// without credit ever excluding a member outright the way a day off does. The RAW pick
    /// (ratio without credit) is computed too; if the two disagree AND the raw pick actually had
    /// credit to spend, the difference is attributed to that credit being consumed - see
    /// <see cref="PickResult"/>. A tie, or a raw pick with no credit, means nothing was
    /// consumed - credit did not change anything, so nothing should look like it did.
    /// </remarks>
    public static PickResult? PickNext(
        Household household,
        TaskDefinition definition,
        IReadOnlyDictionary<Guid, int> assignedMinutesByMember,
        IReadOnlyDictionary<Guid, int> assignedMinutesTodayOnDate,
        DateOnly today,
        IReadOnlySet<(Guid MemberId, DateOnly Date)> daysOff,
        IReadOnlyDictionary<Guid, int> creditMinutes)
    {
        var eligible = EligibleMembers(household, definition, today, daysOff);

        if (eligible.Count == 0)
        {
            return null;
        }

        var withRoomToday = eligible
            .Where(member => HasRoomToday(member, today, assignedMinutesTodayOnDate, definition.EstimatedMinutes))
            .ToList();

        // Nobody having room left today is not a reason to leave the task unassigned - it still
        // needs an owner, just picked by ratio alone as before.
        var pool = withRoomToday.Count > 0 ? withRoomToday : eligible;

        var pick = Choose(pool, definition, assignedMinutesByMember, creditMinutes);
        var rawPick = Choose(pool, definition, assignedMinutesByMember, credit: null);

        if (pick.Id != rawPick.Id && creditMinutes.GetValueOrDefault(rawPick.Id) > 0)
        {
            var consumed = Math.Min(creditMinutes.GetValueOrDefault(rawPick.Id), definition.EstimatedMinutes);
            return new PickResult(pick.Id, rawPick.Id, consumed);
        }

        return new PickResult(pick.Id, ConsumedFromMemberId: null, ConsumedMinutes: 0);
    }

    private static HouseholdMember Choose(
        IReadOnlyList<HouseholdMember> pool,
        TaskDefinition definition,
        IReadOnlyDictionary<Guid, int> assignedMinutesByMember,
        IReadOnlyDictionary<Guid, int>? credit)
        => pool
            .OrderBy(member => LoadRatio(member, assignedMinutesByMember, credit))
            // A tie (most commonly: everyone still at their starting ratio) defers to whichever
            // member the household explicitly named, if any, before falling back to a stable,
            // arbitrary-but-deterministic order.
            .ThenBy(member => member.Id == definition.DefaultResponsibleMemberId ? 0 : 1)
            .ThenBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .First();

    /// <summary>
    /// Active, not-paused-on-<paramref name="date"/>, not-off-on-<paramref name="date"/>
    /// members, narrowed to adults when <see cref="TaskDefinition.RequiresAdult"/> is set
    /// (falling back to the full list if that would leave nobody eligible). Shared with
    /// <see cref="RebalanceTaskAssignments"/> so a reassignment can never land on someone the
    /// live picker itself would have refused. A day off is a hard exclusion, same as being
    /// paused or inactive - unlike "tid i förväg" (see <see cref="PickNext"/>'s remarks), which
    /// only ever biases who gets picked among those still eligible, never excludes anyone.
    /// </summary>
    internal static IReadOnlyList<HouseholdMember> EligibleMembers(
        Household household, TaskDefinition definition, DateOnly date, IReadOnlySet<(Guid MemberId, DateOnly Date)> daysOff)
    {
        var eligible = household.Members
            .Where(member => member.IsActive && !member.IsPausedOn(date) && !daysOff.Contains((member.Id, date)))
            .OrderBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .ToList();

        if (definition.RequiresAdult)
        {
            // A soft preference: if every active member happens to be a child (or nobody's
            // role is known), falling back to the full list keeps the task assignable rather
            // than stuck with no eligible candidate.
            var adults = eligible.Where(member => member.Role != HouseholdRole.ChildOrTeen).ToList();

            if (adults.Count > 0)
            {
                eligible = adults;
            }
        }

        return eligible;
    }

    /// <summary>Whether assigning a <paramref name="taskMinutes"/> task to <paramref name="member"/> on
    /// <paramref name="date"/> would stay within their own budget for that day, given what
    /// <paramref name="assignedMinutesOnDate"/> already has them down for.</summary>
    internal static bool HasRoomToday(
        HouseholdMember member, DateOnly date, IReadOnlyDictionary<Guid, int> assignedMinutesOnDate, int taskMinutes)
    {
        var capacityToday = member.WeeklyTimeBudget.MinutesFor(date.DayOfWeek);
        var alreadyAssignedToday = assignedMinutesOnDate.GetValueOrDefault(member.Id);

        return alreadyAssignedToday + taskMinutes <= capacityToday;
    }

    /// <summary>
    /// How loaded this member is, relative to how much time they normally have. Outstanding
    /// "tid i förväg" credit (<paramref name="credit"/>, <c>null</c> when the raw, credit-less
    /// ratio is wanted - see <see cref="PickNext"/>) is added to their assigned minutes first,
    /// so it biases the ratio the same way real assigned work would. A member with no declared
    /// weekly budget yet (all zeros - the default for a newly-added member) has no meaningful
    /// share to divide by; falling back to their raw assigned-plus-credit minutes still ranks
    /// them sensibly against other zero-budget members without a division by zero.
    /// </summary>
    private static double LoadRatio(
        HouseholdMember member, IReadOnlyDictionary<Guid, int> assignedMinutesByMember, IReadOnlyDictionary<Guid, int>? credit)
    {
        var assignedMinutes = assignedMinutesByMember.GetValueOrDefault(member.Id)
            + (credit?.GetValueOrDefault(member.Id) ?? 0);
        var capacity = member.WeeklyTimeBudget.TotalWeeklyMinutes;

        return capacity > 0 ? (double)assignedMinutes / capacity : assignedMinutes;
    }
}
