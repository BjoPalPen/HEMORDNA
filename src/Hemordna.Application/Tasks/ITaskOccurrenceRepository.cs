using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Concrete scheduled instances of tasks.</summary>
public interface ITaskOccurrenceRepository
{
    Task AddAsync(TaskOccurrence occurrence, CancellationToken cancellationToken);

    Task<TaskOccurrence?> FindByIdAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken);

    Task UpdateAsync(TaskOccurrence occurrence, CancellationToken cancellationToken);

    /// <summary>
    /// The <see cref="TaskOccurrence.OriginalScheduledDate"/> of the most recently scheduled
    /// occurrence for this definition, or null if none exist yet. Used to find where automatic
    /// recurrence generation left off, regardless of status or later deferrals.
    /// </summary>
    Task<DateOnly?> FindMostRecentOriginalDateAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// When this definition was last marked done, or null if never. Used for "as needed" tasks,
    /// which become due a fixed number of days after their last completion rather than on a
    /// calendar cadence - see TaskDefinition.StaleAfterDays.
    /// </summary>
    Task<DateTimeOffset?> FindMostRecentCompletedAtAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>Whether this definition already has an outstanding (planned, not yet done) occurrence.</summary>
    Task<bool> HasOutstandingAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether this definition already has an outstanding occurrence currently sitting ON
    /// <paramref name="date"/> - regardless of what date it was originally generated for. Used
    /// to guard calendar-recurrence generation against creating a second occurrence for a slot
    /// an existing one has already been moved onto (e.g. by <c>RebalanceSchedule</c>, or a
    /// household member manually deferring it there) - see
    /// <see cref="Hemordna.Application.Tasks.EnsureOccurrencesGenerated"/>.
    /// </summary>
    Task<bool> HasOutstandingOnDateAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Outstanding occurrences of this definition scheduled on or before <paramref name="onOrBefore"/>,
    /// tracked so a caller can reschedule them (see <see cref="TaskOccurrence.DeferTo"/>) and
    /// save the change - unlike the other lookups here, which only ever read.
    /// </summary>
    Task<IReadOnlyList<TaskOccurrence>> ListOutstandingOnOrBeforeAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken);

    /// <summary>
    /// Who already has work in each room on this date, keyed by area id - what
    /// <see cref="RotationPicker"/> needs to keep one room's work for one day with one person.
    /// </summary>
    /// <remarks>
    /// Counts <see cref="TaskOccurrenceStatus.Completed"/> as well as
    /// <see cref="TaskOccurrenceStatus.Planned"/>, and that is the whole point: a household
    /// member who finished the small WC at 09:34 has still claimed that room for the day, so
    /// the last floor task must not go to someone else at 14:00. Looking only at what is still
    /// outstanding would miss exactly the case this exists for.
    /// <para>
    /// <see cref="TaskOccurrenceStatus.Skipped"/> is deliberately not counted - "not needed this
    /// time" is not a claim on the room, and nobody went there.
    /// </para>
    /// <para>Tasks with no area of their own ("Övrigt") are absent: there is no room to hold together.</para>
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>> GetMemberIdsByAreaOnDateAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Every outstanding (<see cref="TaskOccurrenceStatus.Planned"/>) occurrence across the
    /// whole household, regardless of definition - tracked, so <see cref="RebalanceTaskAssignments"/>
    /// can reassign <see cref="TaskOccurrence.AssignedMemberId"/> on the ones that need it and
    /// persist every change in one pass. Completed and skipped occurrences are never included -
    /// see <see cref="TaskOccurrenceStatus"/>.
    /// </summary>
    Task<IReadOnlyList<TaskOccurrence>> ListOutstandingByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken);
}
