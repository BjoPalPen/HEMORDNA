using Hemordna.Domain.Reminders;

namespace Hemordna.Api.Contracts;

/// <summary>A member's own appointment - see docs/PRODUCT.md §11. Never returned for anyone but
/// its owner; see ReminderEndpoints for how that boundary is enforced. Carries its own
/// <see cref="Visibility"/> so the owner can see and confirm the level they picked - that field
/// is never present on <see cref="HouseholdReminderResponse"/>, the type returned to anyone
/// else.</summary>
public sealed record ReminderResponse(
    Guid Id,
    string Title,
    string? Location,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    int? TravelMinutes,
    ReminderStatus Status,
    DateTimeOffset CreatedAt,
    ReminderVisibility Visibility);

/// <summary>
/// <see cref="Visibility"/> is <c>null</c> when the caller did not name a level, which
/// <c>CreateReminder</c> turns into <see cref="ReminderVisibility.Private"/> - see that use
/// case's own remarks.
/// </summary>
public sealed record CreateReminderRequest(
    string? Title,
    string? Location,
    DateOnly? Date,
    TimeOnly? TimeOfDay,
    int? TravelMinutes,
    ReminderVisibility? Visibility);

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
/// One other household member's reminder as it is allowed to appear to someone who is not its
/// owner - see <c>Hemordna.Application.Reminders.HouseholdReminderView</c>, which this type
/// mirrors field for field on the wire. Deliberately excludes <c>Location</c> (never shared at
/// any <see cref="ReminderVisibility"/> level), <c>Status</c> (a checked-off time must look
/// identical to any other time to someone else - see docs/PRODUCT.md §8) and
/// <c>TravelMinutes</c>/<c>CreatedAt</c> (owner-only detail with no use outside the owner's own
/// view). Do not add any of those fields back here without first re-reading why they were left
/// out - this type is deliberately narrower than <see cref="ReminderResponse"/>, not an
/// oversight.
/// </summary>
public sealed record HouseholdReminderResponse(
    Guid Id,
    Guid MemberId,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    string? Title);
