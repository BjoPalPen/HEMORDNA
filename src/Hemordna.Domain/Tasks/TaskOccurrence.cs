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
        DateTimeOffset createdAt,
        bool addedAsExtra)
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
        AddedAsExtra = addedAsExtra;
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

    /// <summary>
    /// True when this occurrence was created through the "Extra uppgift" flow - a genuinely
    /// new, one-off task someone added to their own day, not something the household already
    /// planned for. Drives time credit (<c>MemberTimeCredit.Reason.ExtraTask</c>): completing
    /// an extra task earns credit the same way completing something ahead of its own due date
    /// does, since both mean "I did more than my day already called for". Set only at creation
    /// time - there is no domain operation to change it afterwards, since an occurrence's origin
    /// does not change once it exists.
    /// </summary>
    public bool AddedAsExtra { get; private set; }

    /// <summary>True while the occurrence still needs doing.</summary>
    public bool IsOutstanding => Status == TaskOccurrenceStatus.Planned;

    internal static TaskOccurrence Create(
        TaskDefinition definition, DateOnly date, DateTimeOffset createdAt, bool addedAsExtra = false)
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
            createdAt,
            addedAsExtra);

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

    /// <summary>
    /// Pulls a not-yet-due task onto <paramref name="today"/> - "jobba i förväg": the member
    /// chose to do their own, genuinely future work now rather than waiting. Only
    /// <see cref="ScheduledDate"/> moves; <see cref="OriginalScheduledDate"/> is untouched, so
    /// this can never look overdue (<see cref="IsOverdueOn"/> compares against
    /// <see cref="OriginalScheduledDate"/>, which stays in the future) and never earns the
    /// "kvarlämnat" treatment an actually overdue task gets from the rebalance use case
    /// (<c>RebalanceTaskAssignments</c>, in <c>Hemordna.Application</c>).
    /// </summary>
    public void BringForwardTo(DateOnly today)
    {
        EnsureOutstanding("brought forward");

        if (today >= ScheduledDate)
        {
            throw new DomainException("Only a task not yet due can be brought forward.");
        }

        ScheduledDate = today;
    }

    /// <summary>
    /// True when this occurrence sits on <paramref name="date"/> only because it was pulled
    /// forward from a later date it was not yet due on - see <see cref="BringForwardTo"/>. Used
    /// to show the "från imorgon"-style chip, and to let the daily planner
    /// (<c>DailyPlanner</c>, in <c>Hemordna.Application</c>) always keep a self-chosen task in
    /// <c>Items</c> rather than bumping it to <c>Unplanned</c> for lack of room.
    /// </summary>
    public bool IsBroughtForwardOn(DateOnly date) => IsOutstanding && ScheduledDate == date && OriginalScheduledDate > date;

    /// <summary>
    /// Puts a brought-forward occurrence back on the date it was originally due -
    /// "Ångra" on the "från imorgon" chip's own undo offer. Deliberately its own operation
    /// rather than a call to <see cref="DeferTo"/>: <see cref="BringForwardTo"/> never checked
    /// <see cref="CanBeDeferred"/> (bringing something forward is not "pushing it later", so a
    /// non-deferrable task can be brought forward same as any other), so undoing that move must
    /// not suddenly require deferability either - it is simply reversing the member's own last
    /// action, not asking for a new, ordinary deferral.
    /// </summary>
    public void UndoBringForward()
    {
        if (!IsBroughtForwardOn(ScheduledDate))
        {
            throw new DomainException("This task was not brought forward, so there is nothing to undo.");
        }

        ScheduledDate = OriginalScheduledDate;
    }

    /// <summary>
    /// Undoes a completion - a slip of the thumb, or a task marked done by mistake, should be
    /// easy to take back without turning into a rewritten history. Only the person who
    /// completed it can undo it, and only within a short window (15 minutes): long enough for
    /// "wait, no" but not long enough to quietly edit what actually happened hours or days
    /// later. Anyone else in the household still just sees the task complete again once the
    /// window closes, or if a different member tries.
    /// </summary>
    public void Reopen(Guid byMemberId, DateTimeOffset now)
    {
        Guard.AgainstEmpty(byMemberId, nameof(byMemberId));

        if (Status != TaskOccurrenceStatus.Completed)
        {
            throw new DomainException($"A task with status '{Status}' cannot be reopened.");
        }

        if (byMemberId != CompletedByMemberId)
        {
            throw new DomainException("Only the member who completed a task can undo it.");
        }

        if (now - CompletedAt > TimeSpan.FromMinutes(15))
        {
            throw new DomainException("A completed task can only be undone within 15 minutes.");
        }

        Status = TaskOccurrenceStatus.Planned;
        CompletedByMemberId = null;
        CompletedAt = null;
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
