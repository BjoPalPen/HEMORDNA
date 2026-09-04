using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Decides who is next for a rotating task: the active member after whoever had the last
/// recorded assignment, cycling back to the first once the list is exhausted.
/// </summary>
internal static class RotationPicker
{
    /// <summary>
    /// The member order is stable (by join date, then id) so the same household always rotates
    /// in the same sequence regardless of query order.
    /// </summary>
    public static Guid? PickNext(Household household, TaskDefinition definition, TaskAssignment? lastAssignment)
    {
        var eligible = household.Members
            .Where(member => member.IsActive)
            .OrderBy(member => member.CreatedAt)
            .ThenBy(member => member.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            return null;
        }

        if (lastAssignment is null)
        {
            return definition.DefaultResponsibleMemberId ?? eligible[0].Id;
        }

        var lastIndex = eligible.FindIndex(member => member.Id == lastAssignment.MemberId);

        // The last-assigned member left the household or was deactivated since: restart at the
        // front rather than throwing, since a stale rotation should not block scheduling.
        return lastIndex < 0 ? eligible[0].Id : eligible[(lastIndex + 1) % eligible.Count].Id;
    }
}
