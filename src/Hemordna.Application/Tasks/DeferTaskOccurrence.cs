using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Pushes a scheduled task to a later date.</summary>
public sealed class DeferTaskOccurrence
{
    private readonly ITaskOccurrenceRepository _occurrences;

    public DeferTaskOccurrence(ITaskOccurrenceRepository occurrences) => _occurrences = occurrences;

    /// <summary>
    /// Moves the occurrence, or returns <c>null</c> when the household has no such occurrence.
    /// The domain rejects deferring something that cannot be deferred, or moving it backwards.
    /// </summary>
    public async Task<TaskOccurrence?> HandleAsync(
        Guid householdId,
        Guid occurrenceId,
        DateOnly newDate,
        CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        occurrence.DeferTo(newDate);

        await _occurrences.UpdateAsync(occurrence, cancellationToken);

        return occurrence;
    }
}
