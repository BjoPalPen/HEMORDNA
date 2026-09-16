namespace Hemordna.Domain.Tasks;

/// <summary>
/// The kind of "visit" a task represents to its room - never stored, never a user choice, always
/// derived fresh from a <see cref="TaskDefinition"/>'s current state (see
/// <see cref="VisitKindClassifier"/>). Replaces the old "one room, one person, one day" rule's
/// blindness to the DIFFERENCE between a quick daily routine and an occasional deep clean - see
/// docs/ARCHITECTURE.md "Beslut: Besökstyp härleds".
/// </summary>
public enum VisitKind
{
    /// <summary>Every day, interval 1 - e.g. wiping the sink, taking out the bin. Rotates or has
    /// a fixed owner as usual; never claims a room and is never bound by the room rule.</summary>
    Routine,

    /// <summary>Not Routine, and <see cref="TaskEffort.Heavy"/> - e.g. scrubbing the shower.</summary>
    DeepClean,

    /// <summary>Everything else: weekly, monthly, a daily task with interval &gt; 1, or "as
    /// needed" - at any effort level up to (not including) Heavy-and-not-Routine.</summary>
    RegularClean
}

/// <summary>Derives a <see cref="VisitKind"/> from a task's own current recurrence and effort. A
/// pure function - never stored, never reads the clock.</summary>
public static class VisitKindClassifier
{
    public static VisitKind Of(TaskDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Of(definition.Recurrence, definition.Effort);
    }

    public static VisitKind Of(RecurrenceRule? recurrence, TaskEffort effort)
    {
        if (recurrence is { Frequency: RecurrenceFrequency.Daily, Interval: 1 })
        {
            return VisitKind.Routine;
        }

        return effort == TaskEffort.Heavy ? VisitKind.DeepClean : VisitKind.RegularClean;
    }
}
