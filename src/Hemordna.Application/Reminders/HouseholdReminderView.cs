namespace Hemordna.Application.Reminders;

/// <summary>
/// One other household member's reminder as it is allowed to appear to someone who is not its
/// owner - the result of <see cref="GetHouseholdReminders"/>. Deliberately unnamed after any
/// screen: the client already has <c>HouseholdResponse.Members</c> to look up a display name from
/// <see cref="MemberId"/> locally, so this type carries no name of its own.
/// <para>
/// Carries exactly <see cref="Id"/>, <see cref="MemberId"/>, <see cref="Date"/>,
/// <see cref="TimeOfDay"/> and <see cref="Title"/> - nothing else. In particular, no
/// <c>Location</c>, no <c>Status</c>, no <c>TravelMinutes</c> and no <c>CreatedAt</c>: those never
/// leave the owner at any <see cref="Hemordna.Domain.Reminders.ReminderVisibility"/> level (see
/// besluten in the reminder-visibility feature). This is a separate, narrower type rather than a
/// reuse of the owner-facing reminder DTO precisely so a future field added there - say, a new
/// owner-only detail - cannot leak into the household view merely by inheriting it; extending this
/// type is a deliberate, visible act instead.
/// </para>
/// </summary>
public sealed record HouseholdReminderView(
    Guid Id,
    Guid MemberId,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    string? Title);
