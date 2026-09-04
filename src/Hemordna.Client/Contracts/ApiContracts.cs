namespace Hemordna.Client.Contracts;

/// <summary>
/// The shapes the API returns. Kept as a small client-side copy rather than sharing the API
/// project: the client depends on the HTTP contract, not on the server's assemblies.
/// </summary>
public sealed record AccessTokenResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? HouseholdId,
    Guid? MemberId);

public sealed record DailyPlanResponse(
    Guid MemberId,
    DateOnly Date,
    int AvailableMinutes,
    int PlannedMinutes,
    int RemainingMinutes,
    int CompletedMinutes,
    IReadOnlyList<PlannedTaskResponse> Items,
    IReadOnlyList<CompletedTaskResponse> Completed,
    IReadOnlyList<UnplannedTaskResponse> Unplanned);

public sealed record CompletedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes);

public sealed record PlannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool IsOverdue);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool CanBeDeferred,
    string Reason);

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<HouseholdMemberResponse> Members,
    IReadOnlyList<AreaResponse> Areas);

public sealed record HouseholdMemberResponse(Guid Id, string DisplayName, bool IsActive);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive);
