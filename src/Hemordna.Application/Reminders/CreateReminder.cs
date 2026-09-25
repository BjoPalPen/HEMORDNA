using Hemordna.Application.Households;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>Books a new, upcoming reminder for its owner. See docs/PRODUCT.md §11.</summary>
public sealed class CreateReminder
{
    private readonly IHouseholdRepository _households;
    private readonly IReminderRepository _reminders;
    private readonly TimeProvider _timeProvider;

    public CreateReminder(
        IHouseholdRepository households, IReminderRepository reminders, TimeProvider timeProvider)
    {
        _households = households;
        _reminders = reminders;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates the reminder, or returns <c>null</c> when the household has no such member - the
    /// same "member must actually belong to this household" check
    /// <see cref="Households.SetMemberAvailability"/> already makes. The domain's own validation
    /// (empty title, date outside the supported calendar, travel time given without a time of
    /// day, ...) is deliberately left to throw uncaught - a real rule violation, not a missing
    /// member. <paramref name="visibility"/> is <c>null</c> when the caller did not name a level -
    /// same as omitting it to <see cref="Reminder.Create"/> directly - and becomes
    /// <see cref="ReminderVisibility.Private"/>, never left for the domain default to resolve
    /// silently: a caller who forgot to send a level should get the same private-by-default
    /// guarantee as one who explicitly asked for it.
    /// <para>
    /// <paramref name="audience"/> and <paramref name="memberIds"/> let a reminder be created
    /// already shared, in one call, instead of a second <see cref="SetReminderAudience"/> round
    /// trip. <c>null</c> <paramref name="audience"/> becomes <see cref="ReminderAudience.Everyone"/>
    /// (the same default <see cref="Reminder.Create"/> itself already starts every reminder at),
    /// and <paramref name="memberIds"/> is treated as empty when omitted. Also returns
    /// <c>null</c>, creating nothing at all, when <paramref name="audience"/> is
    /// <see cref="ReminderAudience.Selected"/> and any id in <paramref name="memberIds"/> is not
    /// currently an active member of this household - <see cref="ReminderAudienceValidation"/>,
    /// the exact same check <see cref="SetReminderAudience"/> makes. Creation must not be a
    /// backdoor around that validation just because it happens to run before the reminder exists
    /// rather than after.
    /// </para>
    /// </summary>
    public async Task<Reminder?> HandleAsync(
        Guid householdId,
        Guid memberId,
        string title,
        string? location,
        DateOnly date,
        TimeOnly? timeOfDay,
        int? travelMinutes,
        ReminderVisibility? visibility,
        ReminderAudience? audience,
        IReadOnlyCollection<Guid>? memberIds,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        var memberBelongsToHousehold = household is not null
            && household.Members.Any(member => member.Id == memberId);

        if (!memberBelongsToHousehold)
        {
            return null;
        }

        var resolvedAudience = audience ?? ReminderAudience.Everyone;
        var resolvedMemberIds = memberIds ?? [];

        if (resolvedAudience == ReminderAudience.Selected && resolvedMemberIds.Count > 0
            && !ReminderAudienceValidation.EveryIdIsAnActiveHouseholdMember(household!, resolvedMemberIds))
        {
            return null;
        }

        var reminder = Reminder.Create(
            householdId, memberId, title, location, date, timeOfDay, _timeProvider.GetUtcNow(),
            travelMinutes, visibility ?? ReminderVisibility.Private);

        reminder.SetAudience(resolvedAudience, resolvedMemberIds);

        await _reminders.AddAsync(reminder, cancellationToken);

        return reminder;
    }
}
