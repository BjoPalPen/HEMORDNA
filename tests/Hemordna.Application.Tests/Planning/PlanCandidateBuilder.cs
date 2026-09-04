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

    public PlanCandidate Build() => new(BuildOccurrence(), _name);

    public TaskOccurrence BuildOccurrence()
    {
        var definition = TaskDefinition.Create(HouseholdId, _name, _estimatedMinutes, CreatedAt);
        definition.ChangePriority(_priority);
        definition.SetCanBeDeferred(_canBeDeferred);
        return definition.ScheduleFor(_scheduledDate, CreatedAt);
    }
}
