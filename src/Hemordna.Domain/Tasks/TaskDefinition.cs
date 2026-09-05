using Hemordna.Domain.Common;

namespace Hemordna.Domain.Tasks;

/// <summary>
/// The household's standing description of a piece of work ("Dammsug vardagsrum"): what it
/// is, roughly how long it takes and who normally owns it.
/// </summary>
/// <remarks>
/// A definition describes the norm. It is never edited to express something temporary - a
/// member being away, or a task being skipped once, is handled on
/// <see cref="TaskOccurrence"/>.
/// <para>
/// <see cref="PreferredWeekday"/> is a soft scheduling hint used when an occurrence is
/// scheduled manually. <see cref="Recurrence"/> is the separate, self-contained rule an
/// automatic generator uses to keep occurrences coming without a human scheduling each one.
/// </para>
/// </remarks>
public sealed class TaskDefinition
{
    private TaskDefinition(
        Guid id,
        Guid householdId,
        string name,
        int estimatedMinutes,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        Name = name;
        EstimatedMinutes = estimatedMinutes;
        Priority = TaskPriority.Normal;
        CanBeDeferred = true;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    /// <summary>The area this work belongs to, if any. Not every task belongs to a room.</summary>
    public Guid? AreaId { get; private set; }

    /// <summary>Expected duration in minutes. Always greater than zero - the planner budgets on it.</summary>
    public int EstimatedMinutes { get; private set; }

    public TaskPriority Priority { get; private set; }

    /// <summary>Who normally owns this task, when the household has agreed on an owner.</summary>
    public Guid? DefaultResponsibleMemberId { get; private set; }

    /// <summary>The weekday the household prefers this to happen on, when it matters.</summary>
    public DayOfWeek? PreferredWeekday { get; private set; }

    /// <summary>Whether an occurrence may be pushed to a later date.</summary>
    public bool CanBeDeferred { get; private set; }

    /// <summary>Whether responsibility rotates between members instead of staying with one.</summary>
    public bool HasRotatingResponsibility { get; private set; }

    /// <summary>Whether the task realistically needs more than one person.</summary>
    public bool RequiresMultiplePeople { get; private set; }

    /// <summary>
    /// Whether this task's rotation should skip members whose role is a child - e.g. washing
    /// windows. Only a soft preference: if every active member is a child, rotation falls back
    /// to the whole household rather than leaving the task with nobody to assign - see
    /// RotationPicker.
    /// </summary>
    public bool RequiresAdult { get; private set; }

    /// <summary>How this task repeats on its own, or null when occurrences are only scheduled by hand.</summary>
    public RecurrenceRule? Recurrence { get; private set; }

    /// <summary>
    /// "As needed": becomes due this many days after it was last completed (or after creation,
    /// if never completed), instead of on a fixed calendar cadence. Independent of
    /// <see cref="Recurrence"/> - a task uses one or the other, not both.
    /// </summary>
    public int? StaleAfterDays { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TaskDefinition Create(
        Guid householdId,
        string name,
        int estimatedMinutes,
        DateTimeOffset createdAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstNonPositive(estimatedMinutes, nameof(estimatedMinutes));

        return new TaskDefinition(
            Guid.NewGuid(),
            householdId,
            Guard.AgainstNullOrWhiteSpace(name, nameof(name)),
            estimatedMinutes,
            createdAt);
    }

    public void Rename(string name) => Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

    public void ChangeDescription(string? description)
        => Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    public void ChangeEstimatedMinutes(int estimatedMinutes)
        => EstimatedMinutes = Guard.AgainstNonPositive(estimatedMinutes, nameof(estimatedMinutes));

    public void ChangePriority(TaskPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, "Not a valid priority.");
        }

        Priority = priority;
    }

    /// <summary>Moves the task to an area of the same household, or removes the area with <c>null</c>.</summary>
    public void AssignToArea(Guid? areaId)
    {
        if (areaId == Guid.Empty)
        {
            throw new ArgumentException("Area identifier must not be empty.", nameof(areaId));
        }

        AreaId = areaId;
    }

    public void SetDefaultResponsibleMember(Guid? memberId)
    {
        if (memberId == Guid.Empty)
        {
            throw new ArgumentException("Member identifier must not be empty.", nameof(memberId));
        }

        DefaultResponsibleMemberId = memberId;
    }

    public void SetPreferredWeekday(DayOfWeek? weekday)
    {
        if (weekday is { } day && !Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(weekday), weekday, "Not a valid weekday.");
        }

        PreferredWeekday = weekday;
    }

    public void SetCanBeDeferred(bool canBeDeferred) => CanBeDeferred = canBeDeferred;

    public void SetRotatingResponsibility(bool rotating) => HasRotatingResponsibility = rotating;

    public void SetRequiresMultiplePeople(bool requiresMultiplePeople)
        => RequiresMultiplePeople = requiresMultiplePeople;

    public void SetRequiresAdult(bool requiresAdult) => RequiresAdult = requiresAdult;

    /// <summary>Sets or clears the automatic recurrence. Does not touch occurrences already scheduled.</summary>
    public void SetRecurrence(RecurrenceRule? recurrence) => Recurrence = recurrence;

    /// <summary>Sets or clears the "as needed" interval. See <see cref="StaleAfterDays"/>.</summary>
    public void SetStaleAfterDays(int? staleAfterDays)
    {
        if (staleAfterDays is { } days)
        {
            Guard.AgainstNonPositive(days, nameof(staleAfterDays));
        }

        StaleAfterDays = staleAfterDays;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>
    /// Creates a concrete instance of this task for a given date. The planning-relevant
    /// fields are copied onto the occurrence so that later edits to the definition do not
    /// silently rewrite work that was already scheduled.
    /// </summary>
    public TaskOccurrence ScheduleFor(DateOnly date, DateTimeOffset createdAt)
    {
        if (!IsActive)
        {
            throw new DomainException($"Task definition '{Name}' is inactive and cannot be scheduled.");
        }

        return TaskOccurrence.Create(this, date, createdAt);
    }
}
