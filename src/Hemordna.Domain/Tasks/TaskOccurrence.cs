using Hemordna.Domain.Common;

namespace Hemordna.Domain.Tasks;

/// <summary>
/// One concrete instance of a task, scheduled for a specific date. This is where everything
/// temporary happens: assignment, deferral, completion and skipping.
/// </summary>
/// <remarks>
/// The planning-relevant fields (<see cref="EstimatedMinutes"/>, <see cref="Priority"/>,
/// <see cref="CanBeDeferred"/>) are snapshots taken from the definition at scheduling time.
/// Editing the definition afterwards must not change work that is already on someone's day.
/// </remarks>
public sealed class TaskOccurrence
{
    private TaskOccurrence(
        Guid id,
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly scheduledDate,
        int estimatedMinutes,
        TaskPriority priority,
        bool canBeDeferred,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        TaskDefinitionId = taskDefinitionId;
        ScheduledDate = scheduledDate;
        OriginalScheduledDate = scheduledDate;
        EstimatedMinutes = estimatedMinutes;
        Priority = priority;
        CanBeDeferred = canBeDeferred;
        Status = TaskOccurrenceStatus.Planned;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid TaskDefinitionId { get; private set; }

    /// <summary>The date this is currently expected to happen. Deferring moves this forward.</summary>
    public DateOnly ScheduledDate { get; private set; }

    /// <summary>The date it was first scheduled for. Used to tell "overdue" from "planned today".</summary>
    public DateOnly OriginalScheduledDate { get; private set; }

    /// <summary>Snapshot of the definition's estimate at scheduling time.</summary>
    public int EstimatedMinutes { get; private set; }

    /// <summary>Snapshot of the definition's priority at scheduling time.</summary>
    public TaskPriority Priority { get; private set; }

    /// <summary>Snapshot of whether this instance may be pushed to a later date.</summary>
    public bool CanBeDeferred { get; private set; }

    public Guid? AssignedMemberId { get; private set; }

    public TaskOccurrenceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid? CompletedByMemberId { get; private set; }

    /// <summary>True while the occurrence still needs doing.</summary>
    public bool IsOutstanding => Status == TaskOccurrenceStatus.Planned;

    internal static TaskOccurrence Create(TaskDefinition definition, DateOnly date, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var occurrence = new TaskOccurrence(
            Guid.NewGuid(),
            definition.HouseholdId,
            definition.Id,
            date,
            definition.EstimatedMinutes,
            definition.Priority,
            definition.CanBeDeferred,
            createdAt);

        if (definition.DefaultResponsibleMemberId is { } responsibleMemberId)
        {
            occurrence.AssignedMemberId = responsibleMemberId;
        }

        return occurrence;
    }

    /// <summary>Gives the task to a member. Only outstanding work can be reassigned.</summary>
    public void AssignTo(Guid memberId)
    {
        Guard.AgainstEmpty(memberId, nameof(memberId));
        EnsureOutstanding("assigned");

        AssignedMemberId = memberId;
    }

    /// <summary>Removes the assignment, leaving the task outstanding but unowned.</summary>
    public void Unassign()
    {
        EnsureOutstanding("unassigned");

        AssignedMemberId = null;
    }

    /// <summary>
    /// Marks the task done. Idempotent: completing an already completed occurrence is a no-op,
    /// so a duplicate request from a second client cannot rewrite who completed it or when.
    /// </summary>
    public void Complete(Guid completedByMemberId, DateTimeOffset completedAt)
    {
        Guard.AgainstEmpty(completedByMemberId, nameof(completedByMemberId));

        if (Status == TaskOccurrenceStatus.Completed)
        {
            return;
        }

        EnsureOutstanding("completed");

        Status = TaskOccurrenceStatus.Completed;
        CompletedByMemberId = completedByMemberId;
        CompletedAt = completedAt;
    }

    /// <summary>
    /// Pushes the task to a later date. The occurrence stays outstanding; only
    /// <see cref="ScheduledDate"/> moves, so <see cref="OriginalScheduledDate"/> still shows
    /// how overdue it is.
    /// </summary>
    public void DeferTo(DateOnly newDate)
    {
        EnsureOutstanding("deferred");

        if (!CanBeDeferred)
        {
            throw new DomainException("This task cannot be deferred.");
        }

        if (newDate <= ScheduledDate)
        {
            throw new DomainException("A task can only be deferred to a later date.");
        }

        ScheduledDate = newDate;
    }

    /// <summary>Drops the task for this date - it was not needed this time.</summary>
    public void Skip()
    {
        if (Status == TaskOccurrenceStatus.Skipped)
        {
            return;
        }

        EnsureOutstanding("skipped");

        Status = TaskOccurrenceStatus.Skipped;
    }

    /// <summary>True when the task was first due before <paramref name="date"/> and is still outstanding.</summary>
    public bool IsOverdueOn(DateOnly date) => IsOutstanding && OriginalScheduledDate < date;

    private void EnsureOutstanding(string action)
    {
        if (Status != TaskOccurrenceStatus.Planned)
        {
            throw new DomainException(
                $"A task with status '{Status}' cannot be {action}.");
        }
    }
}
