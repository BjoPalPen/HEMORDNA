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
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        TaskDefinitionId = taskDefinitionId;
        MemberId = memberId;
        ScheduledDate = scheduledDate;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid TaskDefinitionId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly ScheduledDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TaskAssignment Create(
        Guid householdId,
        Guid taskDefinitionId,
        Guid memberId,
        DateOnly scheduledDate,
        DateTimeOffset createdAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(taskDefinitionId, nameof(taskDefinitionId));
        Guard.AgainstEmpty(memberId, nameof(memberId));

        return new TaskAssignment(Guid.NewGuid(), householdId, taskDefinitionId, memberId, scheduledDate, createdAt);
    }
}
