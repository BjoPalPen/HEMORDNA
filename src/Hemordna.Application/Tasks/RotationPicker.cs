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
/// proportionally, without ever exceeding what their own budget allows on any given day - that
/// cap still lives entirely in <see cref="Planning.DailyPlanner"/>, which this has no bearing on.
/// </remarks>
internal static class RotationPicker
{
    /// <summary>
    /// <paramref name="assignedMinutesByMember"/> must reflect every assignment already made in
    /// the current batch, not just what is in the database - see
    /// <see cref="EnsureOccurrencesGenerated"/>, which generates many occurrences in one pass
    /// and updates this in place after each pick precisely so the second pick in a batch does
    /// not repeat the cold-start mistake the first one would otherwise make.
    /// </summary>
    public static Guid? PickNext(
        Household household,
        TaskDefinition definition,
        IReadOnlyDictionary<Guid, int> assignedMinutesByMember)
    {
        var eligible = household.Members
            .Where(member => member.IsActive)
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

        if (eligible.Count == 0)
        {
            return null;
        }

        return eligible
            .OrderBy(member => LoadRatio(member, assignedMinutesByMember))
            // A tie (most commonly: everyone still at their starting ratio) defers to whichever
            // member the household explicitly named, if any, before falling back to a stable,
            // arbitrary-but-deterministic order.
            .ThenBy(member => member.Id == definition.DefaultResponsibleMemberId ? 0 : 1)
            .ThenBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .First()
            .Id;
    }

    /// <summary>
    /// How loaded this member is, relative to how much time they normally have. A member with
    /// no declared weekly budget yet (all zeros - the default for a newly-added member) has no
    /// meaningful share to divide by; falling back to their raw assigned minutes still ranks
    /// them sensibly against other zero-budget members without a division by zero.
    /// </summary>
    private static double LoadRatio(HouseholdMember member, IReadOnlyDictionary<Guid, int> assignedMinutesByMember)
    {
        var assignedMinutes = assignedMinutesByMember.GetValueOrDefault(member.Id);
        var capacity = member.WeeklyTimeBudget.TotalWeeklyMinutes;

        return capacity > 0 ? (double)assignedMinutes / capacity : assignedMinutes;
    }
}
