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
        TaskOccurrence occurrence, string taskName, string? areaName = null, string? description = null)
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
    }

    public TaskOccurrence Occurrence { get; }

    public string TaskName { get; }

    /// <summary>The area this work belongs to, when it has one - display only.</summary>
    public string? AreaName { get; }

    public string? Description { get; }

    public int EstimatedMinutes => Occurrence.EstimatedMinutes;

    public TaskPriority Priority => Occurrence.Priority;

    public bool CanBeDeferred => Occurrence.CanBeDeferred;
}
