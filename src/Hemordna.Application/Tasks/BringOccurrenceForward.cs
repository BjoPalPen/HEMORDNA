using Hemordna.Application.Realtime;
using Hemordna.Domain.Common;

namespace Hemordna.Application.Tasks;

/// <summary>"Jobba i förväg" for a single occurrence - see <see cref="Domain.Tasks.TaskOccurrence.BringForwardTo"/>
/// for the rules.</summary>
public sealed class BringOccurrenceForward
{
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IHouseholdNotifier _notifier;

    public BringOccurrenceForward(ITaskOccurrenceRepository occurrences, IHouseholdNotifier notifier)
    {
        _occurrences = occurrences;
        _notifier = notifier;
    }

    /// <summary>
    /// Brings the occurrence forward to <paramref name="today"/>, or returns <c>null</c> when
    /// the household has no such occurrence. Only the member it is currently assigned to may
    /// bring it forward - this is "let me do MY OWN future work now", not a way to pull someone
    /// else's task onto your own day. A rule violation (not the caller's own, not yet due, not
    /// outstanding) throws <see cref="DomainException"/> rather than returning <c>false</c>,
    /// same as <see cref="ReopenTaskOccurrence"/>.
    /// </summary>
    public async Task<bool?> HandleAsync(
        Guid householdId,
        Guid occurrenceId,
        Guid callerMemberId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        if (occurrence.AssignedMemberId != callerMemberId)
        {
            throw new DomainException("Only the member a task is assigned to can bring it forward.");
        }

        occurrence.BringForwardTo(today);

        await _occurrences.UpdateAsync(occurrence, cancellationToken);
        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return true;
    }
}
