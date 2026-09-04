using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Marks a scheduled task as done.</summary>
public sealed class CompleteTaskOccurrence
{
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly TimeProvider _timeProvider;

    public CompleteTaskOccurrence(ITaskOccurrenceRepository occurrences, TimeProvider timeProvider)
    {
        _occurrences = occurrences;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Completes the occurrence, or returns <c>null</c> when the household has no such
    /// occurrence. Completing something already completed is a no-op, so two people tapping
    /// the same task cannot overwrite who finished it.
    /// </summary>
    public async Task<TaskOccurrence?> HandleAsync(
        Guid householdId,
        Guid occurrenceId,
        Guid completedByMemberId,
        CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        occurrence.Complete(completedByMemberId, _timeProvider.GetUtcNow());

        await _occurrences.UpdateAsync(occurrence, cancellationToken);

        return occurrence;
    }
}
