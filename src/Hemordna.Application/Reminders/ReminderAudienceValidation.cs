using Hemordna.Domain.Households;

namespace Hemordna.Application.Reminders;

/// <summary>
/// The membership check every use case that saves a <see cref="Domain.Reminders.ReminderAudience.Selected"/>
/// list must make before doing so: each id must name someone who is CURRENTLY an active member of
/// THIS household, not merely any id that happens to parse as a <see cref="Guid"/>. Shared between
/// <see cref="SetReminderAudience"/> and <see cref="CreateReminder"/> so the two cannot drift
/// apart - see <see cref="SetReminderAudience.HandleAsync"/> for why letting
/// <see cref="CreateReminder"/> skip this check would be a silent way around it, a backdoor into
/// saving a share the other use case would have rejected.
/// </summary>
internal static class ReminderAudienceValidation
{
    internal static bool EveryIdIsAnActiveHouseholdMember(
        Household household, IReadOnlyCollection<Guid> memberIds)
        => memberIds.All(memberId => household.Members
            .Any(member => member.Id == memberId && member.IsActive));
}
