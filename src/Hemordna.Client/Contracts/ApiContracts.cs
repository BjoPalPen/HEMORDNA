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
    int EstimatedMinutes,
    string? AreaName);

public sealed record PlannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool IsOverdue,
    string? AreaName,
    string? Description,
    bool CanBeDeferred);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool CanBeDeferred,
    string Reason,
    string? AreaName);

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<HouseholdMemberResponse> Members,
    IReadOnlyList<AreaResponse> Areas);

public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive,
    WeeklyTimeBudgetContract WeeklyTimeBudgetMinutes);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive);

/// <summary>Minutes per weekday. Mirrors the API's contract - see it for the domain mapping.</summary>
public sealed record WeeklyTimeBudgetContract(
    int Monday,
    int Tuesday,
    int Wednesday,
    int Thursday,
    int Friday,
    int Saturday,
    int Sunday);

public sealed record PreferenceResponse(Guid MemberId, string Presentation, string Motivation);

public sealed record RecentActivityResponse(
    Guid OccurrenceId, string TaskName, string MemberDisplayName, DateTimeOffset CompletedAt);

public sealed record MemberDayStatusResponse(Guid MemberId, DateOnly Date, string Status);

public sealed record AddAreaRequest(string Name);

public sealed record AddMemberRequest(string DisplayName, WeeklyTimeBudgetContract? WeeklyTimeBudgetMinutes);

public sealed record CreateTaskRequest(
    string Name,
    int EstimatedMinutes,
    string? Description,
    Guid? AreaId,
    string Priority,
    Guid? DefaultResponsibleMemberId,
    string? PreferredWeekday,
    bool CanBeDeferred,
    bool HasRotatingResponsibility,
    bool RequiresMultiplePeople,
    RecurrenceRuleContract? Recurrence);

public sealed record TaskDefinitionResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? AreaId,
    int EstimatedMinutes,
    string Priority,
    Guid? DefaultResponsibleMemberId,
    string? PreferredWeekday,
    bool CanBeDeferred,
    bool HasRotatingResponsibility,
    bool RequiresMultiplePeople,
    bool IsActive,
    RecurrenceRuleContract? Recurrence);

/// <summary>
/// How a task repeats. Enum-shaped fields travel as plain strings - see the file header for
/// why the client keeps its own primitive-only copy of the wire contract.
/// </summary>
public sealed record RecurrenceRuleContract(
    string Frequency,
    int Interval,
    DateOnly StartDate,
    string? Weekday,
    string? MonthlyWeek);
