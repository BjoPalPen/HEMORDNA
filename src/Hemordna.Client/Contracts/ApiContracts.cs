namespace Hemordna.Client.Contracts;

/// <summary>
/// The shapes the API returns. Kept as a small client-side copy rather than sharing the API
/// project: the client depends on the HTTP contract, not on the server's assemblies.
/// </summary>
public sealed record AccessTokenResponse(string Token, DateTimeOffset ExpiresAt);

public sealed record PasskeyResponse(string Id, string DeviceLabel, DateTimeOffset CreatedAt);

public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? HouseholdId,
    Guid? MemberId,
    bool CanManageHousehold);

public sealed record DailyPlanResponse(
    Guid MemberId,
    DateOnly Date,
    int AvailableMinutes,
    int PlannedMinutes,
    int RemainingMinutes,
    int CompletedMinutes,
    IReadOnlyList<PlannedTaskResponse> Items,
    IReadOnlyList<CompletedTaskResponse> Completed,
    IReadOnlyList<UnplannedTaskResponse> Unplanned,
    bool IsDayOff);

public sealed record CompletedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string? AreaName,
    Guid? CompletedByMemberId,
    string? Floor);

/// <param name="IsRoutine">True for a daily, interval-1 routine - drives "Rutiner", the leading
/// group on Min dag (Björns beslut: "överst och alltid med").</param>
public sealed record PlannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool IsOverdue,
    string? AreaName,
    string? Description,
    bool CanBeDeferred,
    DateOnly OriginalScheduledDate,
    bool IsRoutine,
    string? Floor);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string Priority,
    bool CanBeDeferred,
    string Reason,
    string? AreaName,
    string? Floor);

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    string InviteCode,
    DateOnly? PausedUntil,
    IReadOnlyList<HouseholdMemberResponse> Members,
    IReadOnlyList<AreaResponse> Areas);

public sealed record JoinHouseholdRequest(string InviteCode);

public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive,
    WeeklyTimeBudgetContract WeeklyTimeBudgetMinutes,
    string? Role,
    DateOnly? PausedUntil,
    bool CanManageHousehold,
    bool HasAccount,
    WeeklyEffortCeilingContract WeeklyEffortCeiling);

/// <summary>The heaviest task effort a member takes on, per weekday. Mirrors the API's contract -
/// see it for the domain mapping. Days travel as plain strings, same as every other enum-shaped
/// field the client carries - see this file's header.</summary>
public sealed record WeeklyEffortCeilingContract(
    string Monday,
    string Tuesday,
    string Wednesday,
    string Thursday,
    string Friday,
    string Saturday,
    string Sunday);

/// <summary>Pauses through and including <c>Until</c>, or resumes immediately when it is <c>null</c>.</summary>
public sealed record PauseRequest(DateOnly? Until);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive, DateOnly? PausedUntil, string? Floor);

/// <summary>Minutes per weekday. Mirrors the API's contract - see it for the domain mapping.</summary>
public sealed record WeeklyTimeBudgetContract(
    int Monday,
    int Tuesday,
    int Wednesday,
    int Thursday,
    int Friday,
    int Saturday,
    int Sunday);

public sealed record PreferenceResponse(Guid MemberId, string Presentation, string Motivation, bool ShowTimeLevel);

public sealed record RecentActivityResponse(
    Guid OccurrenceId, string TaskName, string MemberDisplayName, DateTimeOffset CompletedAt);

public sealed record DailyActivitySummaryResponse(DateOnly Date, int CompletedCount, int TotalCount);

public sealed record MemberDayStatusResponse(Guid MemberId, DateOnly Date, string Status, bool IsDayOff);

/// <summary>How many of the member's own occurrences on the day off actually moved - see
/// SetMemberDayOff.</summary>
public sealed record DayOffResponse(int BroughtForward, int Deferred, DateOnly? DeferredTo);

public sealed record MemberDayOffResponse(DateOnly Date);

/// <summary>A member's own "tid i förväg" balance - always the calling member's own, see
/// GetMemberTimeCredit.</summary>
public sealed record TimeCreditResponse(int Minutes);

public sealed record AddAreaRequest(string Name, string? Floor = null);

/// <summary>Moves an area to a (possibly different) floor, or clears it when <c>Floor</c> is
/// null - mirrors the API's own <c>SetAreaFloorRequest</c>.</summary>
public sealed record SetAreaFloorRequest(string? Floor);

public sealed record AddMemberRequest(
    string DisplayName, WeeklyTimeBudgetContract? WeeklyTimeBudgetMinutes, string? Role = null);

public sealed record SetMemberRoleRequest(
    string? Role,
    WeeklyTimeBudgetContract? WeeklyTimeBudgetMinutes = null,
    WeeklyEffortCeilingContract? WeeklyEffortCeiling = null);

/// <summary>Grants or removes a member's ability to manage the household - see docs/ARCHITECTURE.md
/// "Beslut: Vem får ändra vad".</summary>
public sealed record SetCanManageHouseholdRequest(bool CanManageHousehold);

/// <summary>"Extra uppgift" on Min dag. <c>Today</c> lets the client name its own local date -
/// see CompleteOccurrenceAsync's own remarks for why.</summary>
public sealed record CreateExtraTaskRequest(
    string Name, int EstimatedMinutes, string? Description, Guid? AreaId, DateOnly? Today = null);

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
    RecurrenceRuleContract? Recurrence,
    int? StaleAfterDays = null,
    bool RequiresAdult = false,
    string Effort = "Medium",
    bool AutoPlaceWeekday = false);

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
    bool RequiresAdult,
    bool IsActive,
    RecurrenceRuleContract? Recurrence,
    int? StaleAfterDays,
    string Effort);

/// <summary>A scheduled instance of a task - only used today for the "Extra uppgift" response,
/// since every other occurrence-returning call the client already made did not need the body.</summary>
public sealed record TaskOccurrenceResponse(
    Guid Id,
    Guid TaskDefinitionId,
    DateOnly ScheduledDate,
    DateOnly OriginalScheduledDate,
    int EstimatedMinutes,
    string Priority,
    bool CanBeDeferred,
    Guid? AssignedMemberId,
    string Status);

/// <summary>"Planera veckan" - one visit's placement, for the preview. No per-person numbers -
/// see the API's own contract. <c>VisitKey</c> is echoed back when moving this visit to another
/// day (Björns krav, "Planera veckan går att ändra").</summary>
public sealed record WeeklyPlanVisitResponse(Guid VisitKey, Guid? AreaId, string? AreaName, string VisitKind, int Minutes, bool IsLocked);

public sealed record WeeklyPlanDayResponse(
    string Day, int MinutesBefore, int MinutesAfter, IReadOnlyList<WeeklyPlanVisitResponse> Visits);

/// <summary><c>EffortWarningDays</c>: weekdays where a move just made lands on a day nobody's
/// effort ceiling can handle - shown as a calm notice, never blocked.</summary>
public sealed record WeeklyPlanResponse(IReadOnlyList<WeeklyPlanDayResponse> Days, IReadOnlyList<string> EffortWarningDays);

/// <summary>One visit moved to another weekday - see the API's own contract.</summary>
public sealed record WeeklyPlanMoveRequest(Guid VisitKey, string Weekday);

public sealed record ApplyWeeklyPlanResponse(int ChangedTaskCount);

public sealed record RebalanceScheduleResponse(int ChangedTaskCount);

public sealed record RebalanceAssignmentsResponse(int ChangedOccurrenceCount);

public sealed record RefreshRoleBudgetsResponse(int UpdatedMemberCount);

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
