using Hemordna.Application.Planning;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Api.Contracts;

public sealed record CreateHouseholdRequest(string? Name);

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

public sealed record AddMemberRequest(string? DisplayName, WeeklyTimeBudgetContract? WeeklyTimeBudgetMinutes);

public sealed record AddAreaRequest(string? Name);

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
    RecurrenceRuleContract? Recurrence = null);

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
    bool IsActive,
    RecurrenceRuleContract? Recurrence);

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
    int EstimatedMinutes);

public sealed record PlannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool IsOverdue);

public sealed record UnplannedTaskResponse(
    Guid OccurrenceId,
    Guid TaskDefinitionId,
    string Name,
    int EstimatedMinutes,
    TaskPriority Priority,
    bool CanBeDeferred,
    UnplannedReason Reason);
