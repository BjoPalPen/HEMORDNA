using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Planning;

/// <summary>
/// A task instance offered to the planner, paired with the display name the plan needs.
/// The name comes from the task definition; the planner itself never loads definitions,
/// which keeps it a pure function over its input.
/// </summary>
public sealed record PlanCandidate
{
    public PlanCandidate(
        TaskOccurrence occurrence,
        string taskName,
        string? areaName = null,
        string? description = null,
        bool isRoutine = false,
        TaskEffort effort = TaskEffort.Medium)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        if (string.IsNullOrWhiteSpace(taskName))
        {
            throw new ArgumentException("Task name must not be null or whitespace.", nameof(taskName));
        }

        Occurrence = occurrence;
        TaskName = taskName.Trim();
        AreaName = areaName;
        Description = description;
        IsRoutine = isRoutine;
        Effort = effort;
    }

    public TaskOccurrence Occurrence { get; }

    public string TaskName { get; }

    /// <summary>The area this work belongs to, when it has one - drives DailyPlanner's room/
    /// floor clustering tie-break (see <see cref="TaskCluster"/>) in addition to display.</summary>
    public string? AreaName { get; }

    public string? Description { get; }

    /// <summary>
    /// True when the underlying task definition classifies as <see cref="VisitKind.Routine"/>
    /// (daily, interval 1) - see <see cref="VisitKindClassifier"/>, not redefined here. Drives
    /// <see cref="DailyPlanner"/>'s "rutiner först" ordering rule (Björns beslut: "överst och
    /// alltid med") - a rotation/completion flag the occurrence itself does not carry, so it is
    /// supplied by whoever builds the candidate (see <c>PlanCandidateQuery</c>, which already
    /// joins to the task definition for its name/area).
    /// </summary>
    public bool IsRoutine { get; }

    public int EstimatedMinutes => Occurrence.EstimatedMinutes;

    public TaskPriority Priority => Occurrence.Priority;

    public bool CanBeDeferred => Occurrence.CanBeDeferred;

    /// <summary>
    /// How much this task takes out of whoever does it - read live from the task definition,
    /// same as <see cref="IsRoutine"/>'s own classification (see <see cref="TaskDefinition.Effort"/>'s
    /// own remarks: effort is never snapshotted onto the occurrence). Drives
    /// <see cref="DailyPlanner"/>'s "orkvalet styr dagens tyngd" ceiling check - see
    /// docs/ARCHITECTURE.md.
    /// </summary>
    public TaskEffort Effort { get; }
}
