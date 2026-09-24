using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Reminders;

/// <summary>
/// Reads other household members' visible reminders in a date range - Vecka's "Andras tider den
/// här veckan" section. Deliberately its own use case, its own repository call
/// (<see cref="IReminderRepository.ListVisibleForOthersInRangeAsync"/>) and its own read model
/// (<see cref="HouseholdReminderView"/>) rather than a union with <see cref="GetOwnReminders"/>:
/// the owner-facing reminder DTO carries <c>Location</c> and <c>TravelMinutes</c>, so a union
/// would turn every future field added there into a potential leak to the rest of the household.
/// <para>
/// Titles are nulled out for <see cref="ReminderVisibility.BusyOnly"/> HERE, not in the endpoint
/// or the UI - see <see cref="HandleAsync"/>. Doing it in this use case means the privacy rule is
/// covered by an Application test rather than resting on every caller above it remembering to
/// blank the title.
/// </para>
/// </summary>
public sealed class GetHouseholdReminders
{
    private readonly IReminderRepository _reminders;

    public GetHouseholdReminders(IReminderRepository reminders) => _reminders = reminders;

    /// <summary>
    /// <paramref name="callerMemberId"/> is excluded from the result - see
    /// <see cref="IReminderRepository.ListVisibleForOthersInRangeAsync"/> - and is never treated
    /// as an owner check the way <see cref="ChangeReminderVisibility"/> and
    /// <see cref="ChangeReminderTitle"/> use one: there is no single reminder id here to own, only
    /// a household-wide read.
    /// </summary>
    public async Task<IReadOnlyList<HouseholdReminderView>> HandleAsync(
        Guid householdId,
        Guid callerMemberId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var reminders = await _reminders.ListVisibleForOthersInRangeAsync(
            householdId, callerMemberId, fromDate, toDate, cancellationToken);

        return [.. reminders.Select(reminder => new HouseholdReminderView(
            reminder.Id,
            reminder.MemberId,
            reminder.Date,
            reminder.TimeOfDay,
            reminder.Visibility == ReminderVisibility.Household ? reminder.Title : null))];
    }
}
