using Hemordna.Domain.Reminders;

namespace Hemordna.Api.Contracts;

/// <summary>A member's own appointment - see docs/PRODUCT.md §11. Never returned for anyone but
/// its owner; see ReminderEndpoints for how that boundary is enforced. Carries its own
/// <see cref="Visibility"/> and <see cref="Audience"/> so the owner can see and confirm what they
/// picked - neither field is ever present on <see cref="HouseholdReminderResponse"/>, the type
/// returned to anyone else; see that type's own remarks for why.</summary>
public sealed record ReminderResponse(
    Guid Id,
    string Title,
    string? Location,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    int? TravelMinutes,
    ReminderStatus Status,
    DateTimeOffset CreatedAt,
    ReminderVisibility Visibility,
    ReminderAudience Audience,
    IReadOnlyList<Guid> SharedWithMemberIds);

/// <summary>
/// <see cref="Visibility"/> is <c>null</c> when the caller did not name a level, which
/// <c>CreateReminder</c> turns into <see cref="ReminderVisibility.Private"/> - see that use
/// case's own remarks. <see cref="Audience"/> and <see cref="MemberIds"/> let a reminder be
/// created already shared with specific members, in this same call, instead of a second request
/// to the audience route right after - <c>null</c> <see cref="Audience"/> becomes
/// <see cref="ReminderAudience.Everyone"/>, same as leaving it out of
/// <c>Reminder.SetAudience</c> would not do on its own; see <c>CreateReminder</c>'s own remarks
/// for the exact default and for why an invalid id in <see cref="MemberIds"/> creates nothing at
/// all rather than silently dropping just that id.
/// </summary>
public sealed record CreateReminderRequest(
    string? Title,
    string? Location,
    DateOnly? Date,
    TimeOnly? TimeOfDay,
    int? TravelMinutes,
    ReminderVisibility? Visibility,
    ReminderAudience? Audience,
    IReadOnlyCollection<Guid>? MemberIds);

public sealed record ChangeReminderTitleRequest(string? Title);

/// <summary>Blank clears the location - see <c>Reminder.ChangeLocation</c>.</summary>
public sealed record ChangeReminderLocationRequest(string? Location);

public sealed record MoveReminderRequest(DateOnly? Date, TimeOnly? TimeOfDay);

/// <summary><c>null</c> clears the travel time - see <c>Reminder.SetTravelMinutes</c>.</summary>
public sealed record SetReminderTravelMinutesRequest(int? TravelMinutes);

/// <summary>A required level to change to - <c>null</c> is rejected by the endpoint, unlike
/// <see cref="ChangeReminderLocationRequest"/>'s blank-clears-it convention, because there is no
/// "no level" state to move to: every reminder always has exactly one of the three
/// <see cref="ReminderVisibility"/> values.</summary>
public sealed record SetReminderVisibilityRequest(ReminderVisibility? Visibility);

/// <summary>
/// A required audience to change to, and the member ids for it when it is
/// <see cref="ReminderAudience.Selected"/> - <c>null</c> <see cref="Audience"/> is rejected by
/// the endpoint, same reasoning as <see cref="SetReminderVisibilityRequest"/>. <c>null</c>
/// <see cref="MemberIds"/> is treated as an empty list, not an error - "no one named yet" is the
/// deliberate, fail-closed <see cref="ReminderAudience.Selected"/> state, not a missing field.
/// </summary>
public sealed record SetReminderAudienceRequest(ReminderAudience? Audience, IReadOnlyCollection<Guid>? MemberIds);

/// <summary>
/// One other household member's reminder as it is allowed to appear to someone who is not its
/// owner - see <c>Hemordna.Application.Reminders.HouseholdReminderView</c>, which this type
/// mirrors field for field on the wire. Deliberately excludes <c>Location</c> (never shared at
/// any <see cref="ReminderVisibility"/> level), <c>Status</c> (a checked-off time must look
/// identical to any other time to someone else - see docs/PRODUCT.md §8),
/// <c>TravelMinutes</c>/<c>CreatedAt</c> (owner-only detail with no use outside the owner's own
/// view), and - since the reminder-audience feature - <see cref="ReminderResponse.Audience"/> and
/// any list of who else the time is shared with. Do not add any of those fields back here without
/// first re-reading why they were left out - this type is deliberately narrower than
/// <see cref="ReminderResponse"/>, not an oversight.
/// <para>
/// WHO can see this particular reader's copy of the reminder is exactly the fact
/// <c>Hemordna.Application.Reminders.IReminderRepository.ListVisibleForOthersInRangeAsync</c>
/// already resolved by including (or not including) this row in the response in the first place.
/// A recipient list on the row itself would be a second, redundant way to answer the same
/// question - and a new leak of its own kind: seeing "delad med Anna och Emma" tells a reader
/// something about Anna's and Emma's relationship to the owner that they were never given
/// permission to see. A recipient has no legitimate reason to know who the other recipients are.
/// </para>
/// </summary>
public sealed record HouseholdReminderResponse(
    Guid Id,
    Guid MemberId,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    string? Title);
