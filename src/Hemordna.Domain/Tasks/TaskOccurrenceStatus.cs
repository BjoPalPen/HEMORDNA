namespace Hemordna.Domain.Tasks;

/// <summary>
/// Lifecycle of a single scheduled instance of a task.
/// </summary>
/// <remarks>
/// Deferring is intentionally not a status: it moves <see cref="TaskOccurrence.ScheduledDate"/>
/// forward and the occurrence stays <see cref="Planned"/>. A separate "Deferred" status would
/// make "is this still outstanding?" ambiguous.
/// </remarks>
public enum TaskOccurrenceStatus
{
    /// <summary>Outstanding: scheduled but not yet done.</summary>
    Planned = 0,

    /// <summary>Done.</summary>
    Completed = 1,

    /// <summary>Consciously dropped for this date - "not needed this time".</summary>
    Skipped = 2
}
