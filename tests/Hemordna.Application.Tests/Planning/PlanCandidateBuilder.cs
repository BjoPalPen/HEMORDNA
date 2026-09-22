using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

/// <summary>
/// Builds planner candidates for tests. All dates are fixed and passed in explicitly - no
/// test may depend on the current date.
/// </summary>
internal sealed class PlanCandidateBuilder
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid HouseholdId = Guid.NewGuid();

    private string _name = "Uppgift";
    private int _estimatedMinutes = 10;
    private TaskPriority _priority = TaskPriority.Normal;
    private bool _canBeDeferred = true;
    private DateOnly _scheduledDate = DailyPlannerTests.Friday;
    private string? _areaName;
    private string? _floor;
    private bool _isRoutine;
    private TaskEffort _effort = TaskEffort.Medium;

    public static PlanCandidateBuilder Task(string name) => new() { _name = name };

    public PlanCandidateBuilder Minutes(int estimatedMinutes)
    {
        _estimatedMinutes = estimatedMinutes;
        return this;
    }

    public PlanCandidateBuilder Priority(TaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    public PlanCandidateBuilder NotDeferrable()
    {
        _canBeDeferred = false;
        return this;
    }

    public PlanCandidateBuilder On(DateOnly date)
    {
        _scheduledDate = date;
        return this;
    }

    public PlanCandidateBuilder DueDaysAgo(int days)
    {
        _scheduledDate = DailyPlannerTests.Friday.AddDays(-days);
        return this;
    }

    public PlanCandidateBuilder InArea(string? areaName)
    {
        _areaName = areaName;
        return this;
    }

    /// <summary>The area's own Floor - read directly by TaskCluster, never parsed out of the
    /// area name any more.</summary>
    public PlanCandidateBuilder OnFloor(string? floor)
    {
        _floor = floor;
        return this;
    }

    /// <summary>Marks this candidate as a <see cref="VisitKind.Routine"/> - see
    /// DailyPlanner's "rutiner först" ordering rule.</summary>
    public PlanCandidateBuilder Routine()
    {
        _isRoutine = true;
        return this;
    }

    /// <summary>How heavy this task is - see DailyPlanner's effort-ceiling check.</summary>
    public PlanCandidateBuilder Effort(TaskEffort effort)
    {
        _effort = effort;
        return this;
    }

    public PlanCandidate Build()
        => new(BuildOccurrence(), _name, _areaName, isRoutine: _isRoutine, effort: _effort, floor: _floor);

    public TaskOccurrence BuildOccurrence()
    {
        var definition = TaskDefinition.Create(HouseholdId, _name, _estimatedMinutes, CreatedAt);
        definition.ChangePriority(_priority);
        definition.SetCanBeDeferred(_canBeDeferred);
        return definition.ScheduleFor(_scheduledDate, CreatedAt);
    }
}
