using Hemordna.Domain.Common;

namespace Hemordna.Domain.Tasks;

/// <summary>
/// A record of who was assigned a <see cref="TaskDefinition"/> for a given date. Exists so
/// rotating responsibility has real history to rotate from, rather than only ever looking at
/// the single most recent <see cref="TaskOccurrence"/> (which may since have been reassigned,
/// completed by someone else, or skipped).
/// </summary>
public sealed class TaskAssignment
{
    private TaskAssignment(
        Guid id,
        Guid householdId,
        Guid taskDefinitionId,
        Guid memberId,
        DateOnly scheduledDate,
        DateTimeOffset createdAt,
        int estimatedMinutes)
    {
        Id = id;
        HouseholdId = householdId;
        TaskDefinitionId = taskDefinitionId;
        MemberId = memberId;
        ScheduledDate = scheduledDate;
        CreatedAt = createdAt;
        EstimatedMinutes = estimatedMinutes;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid TaskDefinitionId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly ScheduledDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Snapshot of the definition's estimated time when this assignment was made - same
    /// reasoning as <see cref="TaskOccurrence"/>'s own snapshots: a later edit to the
    /// definition's estimate must not retroactively change how much load this assignment
    /// counted for when <see cref="RotationPicker"/> balances work across members.
    /// </summary>
    public int EstimatedMinutes { get; private set; }

    public static TaskAssignment Create(
        Guid householdId,
        Guid taskDefinitionId,
        Guid memberId,
        DateOnly scheduledDate,
        DateTimeOffset createdAt,
        int estimatedMinutes)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(taskDefinitionId, nameof(taskDefinitionId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        Guard.AgainstNegative(estimatedMinutes, nameof(estimatedMinutes));

        return new TaskAssignment(
            Guid.NewGuid(), householdId, taskDefinitionId, memberId, scheduledDate, createdAt, estimatedMinutes);
    }
}
