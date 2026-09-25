using Hemordna.Application.Households;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>
/// Sets who, among the household, can see one of the caller's own reminders when its
/// <see cref="Reminder.Visibility"/> is not <see cref="ReminderVisibility.Private"/> - see
/// <see cref="Reminder.SetAudience"/>. <paramref name="audience"/> (`HandleAsync`'s parameter)
/// and its member ids are set together, in one call, so <see cref="Reminder.Shares"/> is always
/// replaced atomically - the list is never left half-updated.
/// </summary>
public sealed class SetReminderAudience
{
    private readonly IHouseholdRepository _households;
    private readonly IReminderRepository _reminders;

    public SetReminderAudience(IHouseholdRepository households, IReminderRepository reminders)
    {
        _households = households;
        _reminders = reminders;
    }

    /// <summary>
    /// Sets the audience, or returns <c>null</c> when the household has no such reminder OR the
    /// reminder belongs to a different member - see <see cref="ChangeReminderTitle"/> for why
    /// those two cases are indistinguishable on purpose: only the owner may ever choose who sees
    /// their own reminder (docs/PRODUCT.md §8).
    /// <para>
    /// Also returns <c>null</c>, and saves nothing, when <paramref name="audience"/> is
    /// <see cref="ReminderAudience.Selected"/> and <paramref name="memberIds"/> names anyone who
    /// is not currently an ACTIVE member of this same household - the same "member must belong
    /// to this household" check <see cref="CreateReminder"/> already makes, extended to require
    /// <c>HouseholdMember.IsActive</c> here: sharing a time with someone who has since left the
    /// household would be a silent leak nobody chose, not a real recipient. This check runs
    /// before anything is saved, so a rejected id leaves the reminder's previous audience and
    /// shares completely untouched, the same all-or-nothing guarantee
    /// <see cref="Reminder.SetAudience"/> itself already gives at the domain level.
    /// </para>
    /// <para>
    /// A rule violation from the domain itself - a cancelled or checked-off reminder, or the
    /// owner naming themselves - is left to throw uncaught, same as
    /// <see cref="ChangeReminderVisibility"/>.
    /// </para>
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        Guid reminderId,
        ReminderAudience audience,
        IReadOnlyCollection<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var reminder = await _reminders.FindByIdAsync(householdId, reminderId, cancellationToken);

        if (reminder is null || reminder.MemberId != callerMemberId)
        {
            return null;
        }

        if (audience == ReminderAudience.Selected && memberIds.Count > 0)
        {
            var household = await _households.FindByIdAsync(householdId, cancellationToken);

            if (household is null
                || !ReminderAudienceValidation.EveryIdIsAnActiveHouseholdMember(household, memberIds))
            {
                return null;
            }
        }

        reminder.SetAudience(audience, memberIds);

        await _reminders.UpdateAsync(reminder, cancellationToken);

        return reminder;
    }
}
