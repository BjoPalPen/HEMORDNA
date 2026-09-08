using Hemordna.Application.Realtime;
using Hemordna.Domain.Common;

namespace Hemordna.Application.Tasks;

/// <summary>Undoes a bring-forward - see <see cref="Domain.Tasks.TaskOccurrence.UndoBringForward"/>
/// for the rules.</summary>
public sealed class UndoBringForward
{
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IHouseholdNotifier _notifier;

    public UndoBringForward(ITaskOccurrenceRepository occurrences, IHouseholdNotifier notifier)
    {
        _occurrences = occurrences;
        _notifier = notifier;
    }

    /// <summary>
    /// Puts the occurrence back on its original date, or returns <c>null</c> when the household
    /// has no such occurrence. Same ownership rule as <see cref="BringOccurrenceForward"/> -
    /// bringing forward never changes who a task is assigned to, so undoing it checks the same
    /// thing: only the member it is still assigned to can put it back. A rule violation throws
    /// <see cref="DomainException"/> rather than returning <c>false</c>, same as
    /// <see cref="ReopenTaskOccurrence"/>.
    /// </summary>
    public async Task<bool?> HandleAsync(
        Guid householdId, Guid occurrenceId, Guid callerMemberId, CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        if (occurrence.AssignedMemberId != callerMemberId)
        {
            throw new DomainException("Only the member a task is assigned to can undo bringing it forward.");
        }

        occurrence.UndoBringForward();

        await _occurrences.UpdateAsync(occurrence, cancellationToken);
        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return true;
    }
}
