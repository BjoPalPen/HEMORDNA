using Hemordna.Application.Realtime;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Undoes a completion - see <see cref="TaskOccurrence.Reopen"/> for the rules.</summary>
public sealed class ReopenTaskOccurrence
{
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IHouseholdNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public ReopenTaskOccurrence(
        ITaskOccurrenceRepository occurrences, IHouseholdNotifier notifier, TimeProvider timeProvider)
    {
        _occurrences = occurrences;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Reopens the occurrence, or returns <c>null</c> when the household has no such
    /// occurrence. A rule violation (wrong person, outside the window, not completed) throws
    /// <see cref="Domain.Common.DomainException"/> rather than returning <c>false</c> - the
    /// caller did something the domain forbids, which is a 409, not a quiet no-op.
    /// </summary>
    public async Task<bool?> HandleAsync(
        Guid householdId,
        Guid occurrenceId,
        Guid byMemberId,
        CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        occurrence.Reopen(byMemberId, _timeProvider.GetUtcNow());

        await _occurrences.UpdateAsync(occurrence, cancellationToken);
        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return true;
    }
}
