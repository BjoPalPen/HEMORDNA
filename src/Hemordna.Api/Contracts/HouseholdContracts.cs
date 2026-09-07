using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Api.Contracts;

public sealed record CreateHouseholdRequest(string? Name);

public sealed record JoinHouseholdRequest(string? InviteCode);

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    string InviteCode,
    DateOnly? PausedUntil,
    IReadOnlyList<HouseholdMemberResponse> Members,
    IReadOnlyList<AreaResponse> Areas);

public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive,
    WeeklyTimeBudgetContract WeeklyTimeBudgetMinutes,
    HouseholdRole? Role,
    DateOnly? PausedUntil);

/// <summary>Pauses through and including <c>Until</c>, or resumes immediately when it is <c>null</c>.</summary>
public sealed record PauseRequest(DateOnly? Until);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive);

public sealed record AddMemberRequest(
    string? DisplayName, WeeklyTimeBudgetContract? WeeklyTimeBudgetMinutes, HouseholdRole? Role = null);

public sealed record SetMemberRoleRequest(HouseholdRole? Role);

public sealed record RebalanceScheduleResponse(int ChangedTaskCount);

/// <summary>How many outstanding occurrences actually changed owner - see <c>RebalanceTaskAssignments</c>.</summary>
public sealed record RebalanceAssignmentsResponse(int ChangedOccurrenceCount);

/// <summary>How many members' budgets were refreshed to the current role preset - see <c>RefreshRolePresetBudgets</c>.</summary>
public sealed record RefreshRoleBudgetsResponse(int UpdatedMemberCount);

public sealed record AddAreaRequest(string? Name);

public sealed record RenameAreaRequest(string? Name);

/// <summary>
/// Minutes per weekday, spelled out. The domain stores these in an array; naming the days in
/// the contract keeps the API self-explanatory without leaking that representation.
/// </summary>
public sealed record WeeklyTimeBudgetContract(
    int Monday,
    int Tuesday,
    int Wednesday,
    int Thursday,
    int Friday,
    int Saturday,
    int Sunday)
{
    internal static WeeklyTimeBudgetContract From(WeeklyTimeBudget budget)
        => new(
            budget.MinutesFor(DayOfWeek.Monday),
            budget.MinutesFor(DayOfWeek.Tuesday),
            budget.MinutesFor(DayOfWeek.Wednesday),
            budget.MinutesFor(DayOfWeek.Thursday),
            budget.MinutesFor(DayOfWeek.Friday),
            budget.MinutesFor(DayOfWeek.Saturday),
            budget.MinutesFor(DayOfWeek.Sunday));

    internal WeeklyTimeBudget ToDomain()
        => WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = Monday,
            [DayOfWeek.Tuesday] = Tuesday,
            [DayOfWeek.Wednesday] = Wednesday,
            [DayOfWeek.Thursday] = Thursday,
            [DayOfWeek.Friday] = Friday,
            [DayOfWeek.Saturday] = Saturday,
            [DayOfWeek.Sunday] = Sunday
        });
}

public sealed record CreateTaskRequest(
    string? Name,
    int EstimatedMinutes,
    string? Description = null,
    Guid? AreaId = null,
    TaskPriority Priority = TaskPriority.Normal,
    Guid? DefaultResponsibleMemberId = null,
    DayOfWeek? PreferredWeekday = null,
    bool CanBeDeferred = true,
    bool HasRotatingResponsibility = false,
    bool RequiresMultiplePeople = false,
    bool RequiresAdult = false,
    RecurrenceRuleContract? Recurrence = null,
    int? StaleAfterDays = null);

/// <summary>Both null means "ingen - schemaläggs för hand" - see TaskDefinition.</summary>
public sealed record UpdateTaskFrequencyRequest(RecurrenceRuleContract? Recurrence, int? StaleAfterDays);

/// <summary>Null means "roterar mellan alla" - see TaskDefinition.SetDefaultResponsibleMember.</summary>
public sealed record UpdateTaskAssignmentRequest(Guid? MemberId);

/// <summary>Null means "Övrigt" (no room) - see TaskDefinition.AssignToArea.</summary>
public sealed record MoveTaskAreaRequest(Guid? AreaId);

public sealed record SetTaskRequiresAdultRequest(bool RequiresAdult);

public sealed record TaskDefinitionResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? AreaId,
    int EstimatedMinutes,
    TaskPriority Priority,
    Guid? DefaultResponsibleMemberId,
    DayOfWeek? PreferredWeekday,
    bool CanBeDeferred,
    bool HasRotatingResponsibility,
    bool RequiresMultiplePeople,
    bool RequiresAdult,
    bool IsActive,
    RecurrenceRuleContract? Recurrence,
    int? StaleAfterDays);

/// <summary>
/// How a task repeats on its own. Mirrors <see cref="RecurrenceRule"/>'s own public shape -
/// see it for what each combination means.
/// </summary>
public sealed record RecurrenceRuleContract(
    RecurrenceFrequency Frequency,
    int Interval,
    DateOnly StartDate,
    DayOfWeek? Weekday,
    WeekOfMonth? MonthlyWeek)
{
    internal RecurrenceRule ToDomain() => this switch
    {
        { MonthlyWeek: { } which } => RecurrenceRule.MonthlyOnWeekday(StartDate, which, Weekday!.Value, Interval),
        { Frequency: RecurrenceFrequency.Weekly } => RecurrenceRule.Weekly(StartDate, Weekday!.Value, Interval),
        { Frequency: RecurrenceFrequency.Monthly } => RecurrenceRule.Monthly(StartDate, Interval),
        _ => RecurrenceRule.Daily(StartDate, Interval)
    };

    internal static RecurrenceRuleContract From(RecurrenceRule rule)
        => new(rule.Frequency, rule.Interval, rule.StartDate, rule.Weekday, rule.MonthlyWeek);
}

public sealed record SetPreferenceRequest(PresentationMode Presentation, MotivationLevel Motivation);

public sealed record RecentActivityResponse(
    Guid OccurrenceId, string TaskName, string MemberDisplayName, DateTimeOffset CompletedAt);

/// <summary>Household-wide (never per-member) count for one day - see IHouseholdDailyActivityQuery.</summary>
public sealed record DailyActivitySummaryResponse(DateOnly Date, int CompletedCount, int TotalCount);

public sealed record MemberDayStatusResponse(Guid MemberId, DateOnly Date, DayStatus Status);

public sealed record PreferenceResponse(Guid MemberId, PresentationMode Presentation, MotivationLevel Motivation);

public sealed record ScheduleOccurrenceRequest(DateOnly? Date, Guid? AssignToMemberId);

public sealed record TaskOccurrenceResponse(
    Guid Id,
    Guid TaskDefinitionId,
    DateOnly ScheduledDate,
    DateOnly OriginalScheduledDate,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool CanBeDeferred,
    Guid? AssignedMemberId,
    TaskOccurrenceStatus Status);

public sealed record SetAvailabilityRequest(DateOnly? Date, int AvailableMinutes);

public sealed record DeferOccurrenceRequest(DateOnly? Date);

public sealed record AvailabilityResponse(Guid MemberId, DateOnly Date, int AvailableMinutes);

/// <summary>One member's day, as "Min dag" renders it.</summary>
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
    TaskPriority Priority,
    bool IsOverdue,
    string? AreaName,
    string? Description,
    bool CanBeDeferred);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool CanBeDeferred,
    UnplannedReason Reason,
    string? AreaName);
