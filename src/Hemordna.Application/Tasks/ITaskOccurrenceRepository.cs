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
}
