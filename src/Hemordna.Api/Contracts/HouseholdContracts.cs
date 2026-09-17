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

/// <param name="HasAccount">
/// Derived from <c>UserId is not null</c> - the raw id is never exposed. The client needs this
/// to know whether offering the "can manage" checkbox for a member even makes sense - see
/// docs/ARCHITECTURE.md "Beslut: Vem får ändra vad".
/// </param>
public sealed record HouseholdMemberResponse(
    Guid Id,
    string DisplayName,
    bool IsActive,
    WeeklyTimeBudgetContract WeeklyTimeBudgetMinutes,
    HouseholdRole? Role,
    DateOnly? PausedUntil,
    bool CanManageHousehold,
    bool HasAccount,
    WeeklyEffortCeilingContract WeeklyEffortCeiling);

/// <summary>Grants or removes a member's ability to manage the household - see docs/ARCHITECTURE.md
/// "Beslut: Vem får ändra vad".</summary>
public sealed record SetCanManageHouseholdRequest(bool CanManageHousehold);

/// <summary>"Extra uppgift" on Min dag - see <c>CreateExtraTask</c>. Deliberately a small subset
/// of <c>CreateTaskRequest</c>: no recurrence, no rotation, no assignment choice - always the
/// caller themselves, today. <c>Today</c> lets the client name its own local date - see
/// <c>CompleteOccurrenceRequest</c> for why; the server's own date is used when it is
/// <c>null</c>.</summary>
public sealed record CreateExtraTaskRequest(
    string? Name, int EstimatedMinutes, string? Description = null, Guid? AreaId = null, DateOnly? Today = null);

/// <summary>Pauses through and including <c>Until</c>, or resumes immediately when it is <c>null</c>.</summary>
public sealed record PauseRequest(DateOnly? Until);

public sealed record AreaResponse(Guid Id, string Name, bool IsActive, DateOnly? PausedUntil);

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

/// <summary>
/// The heaviest task effort a member takes on, per weekday - see <see cref="WeeklyEffortCeiling"/>.
/// Mirrors <see cref="WeeklyTimeBudgetContract"/>'s own shape and reasoning.
/// </summary>
public sealed record WeeklyEffortCeilingContract(
    TaskEffort Monday,
    TaskEffort Tuesday,
    TaskEffort Wednesday,
    TaskEffort Thursday,
    TaskEffort Friday,
    TaskEffort Saturday,
    TaskEffort Sunday)
{
    internal static WeeklyEffortCeilingContract From(WeeklyEffortCeiling ceiling)
        => new(
            ceiling.CeilingFor(DayOfWeek.Monday),
            ceiling.CeilingFor(DayOfWeek.Tuesday),
            ceiling.CeilingFor(DayOfWeek.Wednesday),
            ceiling.CeilingFor(DayOfWeek.Thursday),
            ceiling.CeilingFor(DayOfWeek.Friday),
            ceiling.CeilingFor(DayOfWeek.Saturday),
            ceiling.CeilingFor(DayOfWeek.Sunday));

    internal WeeklyEffortCeiling ToDomain()
        => WeeklyEffortCeiling.Create(new Dictionary<DayOfWeek, TaskEffort>
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

/// <param name="AutoPlaceWeekday">
/// See <c>NewTaskDefinition</c>'s own remarks. When true and <c>Recurrence</c> is Weekly or
/// Monthly, only its <c>Frequency</c>/<c>Interval</c> are read - <c>StartDate</c>/<c>Weekday</c>/
/// <c>MonthlyWeek</c> may be left at any placeholder value, since the server chooses them.
/// </param>
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
    int? StaleAfterDays = null,
    TaskEffort Effort = TaskEffort.Medium,
    bool AutoPlaceWeekday = false);

/// <summary>Both null means "ingen - schemaläggs för hand" - see TaskDefinition.</summary>
public sealed record UpdateTaskFrequencyRequest(RecurrenceRuleContract? Recurrence, int? StaleAfterDays);

/// <summary>Null means "roterar mellan alla" - see TaskDefinition.SetDefaultResponsibleMember.</summary>
public sealed record UpdateTaskAssignmentRequest(Guid? MemberId);

/// <summary>Null means "Övrigt" (no room) - see TaskDefinition.AssignToArea.</summary>
public sealed record MoveTaskAreaRequest(Guid? AreaId);

public sealed record SetTaskRequiresAdultRequest(bool RequiresAdult);

public sealed record ChangeTaskEstimatedMinutesRequest(int EstimatedMinutes);

public sealed record ChangeTaskEffortRequest(TaskEffort Effort);

/// <summary>Null clears the lock. <c>Today</c> lets the client name its own local date - see
/// <c>CompleteOccurrenceRequest</c> for why; it is the date the re-anchored recurrence is
/// anchored from when a weekday is set.</summary>
public sealed record SetPreferredWeekdayRequest(DayOfWeek? Weekday, DateOnly? Today = null);

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
    int? StaleAfterDays,
    TaskEffort Effort);

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

public sealed record SetPreferenceRequest(PresentationMode Presentation, MotivationLevel Motivation, bool ShowTimeLevel);

public sealed record RecentActivityResponse(
    Guid OccurrenceId, string TaskName, string MemberDisplayName, DateTimeOffset CompletedAt);

/// <summary>Household-wide (never per-member) count for one day - see IHouseholdDailyActivityQuery.</summary>
public sealed record DailyActivitySummaryResponse(DateOnly Date, int CompletedCount, int TotalCount);

public sealed record MemberDayStatusResponse(Guid MemberId, DateOnly Date, DayStatus Status, bool IsDayOff);

public sealed record PreferenceResponse(
    Guid MemberId, PresentationMode Presentation, MotivationLevel Motivation, bool ShowTimeLevel);

public sealed record ScheduleOccurrenceRequest(DateOnly? Date, Guid? AssignToMemberId, bool AddedAsExtra = false);

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
    IReadOnlyList<UnplannedTaskResponse> Unplanned,
    bool IsDayOff);

public sealed record CompletedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    string? AreaName,
    Guid? CompletedByMemberId);

/// <param name="IsRoutine">True for a <c>VisitKind.Routine</c> task (daily, interval 1) - see
/// <c>PlanCandidate.IsRoutine</c>. Drives "Rutiner", the client's own leading group on Min dag
/// (Björns beslut: "överst och alltid med"); never affects ordering here, the response already
/// reflects DailyPlanner's own "rutiner först" placement.</param>
public sealed record PlannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool IsOverdue,
    string? AreaName,
    string? Description,
    bool CanBeDeferred,
    DateOnly OriginalScheduledDate,
    bool IsRoutine);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool CanBeDeferred,
    UnplannedReason Reason,
    string? AreaName);

/// <summary><c>Today</c> lets the client name its own local date - see
/// <c>CompleteOccurrenceRequest</c> for why. Used both as the validity-window reference for the
/// day off itself and, for <c>DayOffMode.BringAllForward</c>, as the date work moves TO.</summary>
public sealed record SetDayOffRequest(DayOffMode Mode, DateOnly? Today = null);

/// <summary>See <c>CompleteOccurrenceRequest</c> for why <c>Today</c> is client-suppliable.</summary>
public sealed record BringForwardRequest(DateOnly? Today);

/// <summary>How many of the member's own occurrences on the day off actually moved - see
/// <c>SetMemberDayOff</c>.</summary>
public sealed record DayOffResponse(int BroughtForward, int Deferred, DateOnly? DeferredTo);

public sealed record MemberDayOffResponse(DateOnly Date);

/// <summary><c>Today</c> lets the client name its own local date - the server's own date is
/// used when it is <c>null</c>, see <c>CompleteTaskOccurrence</c>.</summary>
public sealed record CompleteOccurrenceRequest(DateOnly? Today);

/// <summary>A member's own "tid i förväg" balance - see <c>GetMemberTimeCredit</c>. Always the
/// calling member's own balance; there is no way to ask for anyone else's.</summary>
public sealed record TimeCreditResponse(int Minutes);

/// <summary>
/// "Planera veckan" - one visit's placement, for the preview. Deliberately no per-person
/// numbers: this is a planning surface for DAYS, not a comparison between people - see
/// docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen".
/// </summary>
/// <param name="VisitKey">Stable identity for this visit within the current preview - see
/// <c>PlaceableVisit.VisitKey</c>. The client echoes this back when it submits "flytta det här
/// besöket till en annan dag" (Björns krav, "Planera veckan går att ändra").</param>
/// <param name="IsLocked">True when this visit is "Alltid på" a fixed weekday (Björns krav) -
/// either an existing TaskDefinition.PreferredWeekday, or a move the household member just made
/// in this same preview (Björns beslut: a move locks too, once "Använd" is used). The client
/// marks these calmly in the preview instead of explaining why they never move.</param>
public sealed record WeeklyPlanVisitResponse(
    Guid VisitKey, Guid? AreaId, string? AreaName, VisitKind VisitKind, int Minutes, bool IsLocked);

public sealed record WeeklyPlanDayResponse(
    DayOfWeek Day, int MinutesBefore, int MinutesAfter, IReadOnlyList<WeeklyPlanVisitResponse> Visits);

/// <param name="EffortWarningDays">Weekdays where a move the household member just made lands on
/// a day nobody's effort ceiling can actually handle - the move is allowed anyway (a requirement
/// wins, same as an existing lock), but the client shows a calm notice about it instead of
/// staying silent - e.g. "Ingen orkar tunga uppgifter på tisdag".</param>
public sealed record WeeklyPlanResponse(IReadOnlyList<WeeklyPlanDayResponse> Days, IReadOnlyList<DayOfWeek> EffortWarningDays);

/// <summary>One visit the household member moved to a different weekday in the preview - treated
/// exactly like an existing PreferredWeekday lock by the planner (Björns krav: återanvänd
/// lås-mekanismen i planeraren, bygg ingen andra).</summary>
public sealed record WeeklyPlanMoveRequest(Guid VisitKey, DayOfWeek Weekday);

/// <summary><c>Today</c> lets the client name its own local date - see
/// <c>CompleteOccurrenceRequest</c> for why. The server's own date is used when it is
/// <c>null</c>. <c>Moves</c> is the household member's own pending edits to the suggestion -
/// empty or <c>null</c> for the original, unmodified one.</summary>
public sealed record PreviewWeeklyPlanRequest(DateOnly? Today, IReadOnlyList<WeeklyPlanMoveRequest>? Moves);

/// <summary>Same <c>Moves</c> as <see cref="PreviewWeeklyPlanRequest"/> - must be the moves the
/// previewed plan being applied was actually computed with.</summary>
public sealed record ApplyWeeklyPlanRequest(DateOnly? Today, IReadOnlyList<WeeklyPlanMoveRequest>? Moves);

/// <summary>How many task definitions actually got a new weekday or lock - see
/// <c>ApplyWeeklyPlan</c>.</summary>
public sealed record ApplyWeeklyPlanResponse(int ChangedTaskCount);
