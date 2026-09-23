using Hemordna.Domain.Reminders;

namespace Hemordna.Api.Contracts;

/// <summary>A member's own appointment - see docs/PRODUCT.md §11. Never returned for anyone but
/// its owner; see ReminderEndpoints for how that boundary is enforced.</summary>
public sealed record ReminderResponse(
    Guid Id,
    string Title,
    string? Location,
    DateOnly Date,
    TimeOnly? TimeOfDay,
    int? TravelMinutes,
    ReminderStatus Status,
    DateTimeOffset CreatedAt);

public sealed record CreateReminderRequest(
    string? Title, string? Location, DateOnly? Date, TimeOnly? TimeOfDay, int? TravelMinutes);

public sealed record ChangeReminderTitleRequest(string? Title);

/// <summary>Blank clears the location - see <c>Reminder.ChangeLocation</c>.</summary>
public sealed record ChangeReminderLocationRequest(string? Location);

public sealed record MoveReminderRequest(DateOnly? Date, TimeOnly? TimeOfDay);

/// <summary><c>null</c> clears the travel time - see <c>Reminder.SetTravelMinutes</c>.</summary>
public sealed record SetReminderTravelMinutesRequest(int? TravelMinutes);
