using Hemordna.Application.Households;
using Hemordna.Application.Realtime;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>Marks a scheduled task as done.</summary>
public sealed class CompleteTaskOccurrence
{
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly IMemberTimeCreditRepository _credits;
    private readonly IHouseholdNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public CompleteTaskOccurrence(
        ITaskOccurrenceRepository occurrences,
        IMemberTimeCreditRepository credits,
        IHouseholdNotifier notifier,
        TimeProvider timeProvider)
    {
        _occurrences = occurrences;
        _credits = credits;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Completes the occurrence, or returns <c>null</c> when the household has no such
    /// occurrence. Completing something already completed is a no-op, so two people tapping
    /// the same task cannot overwrite who finished it - and earns no credit a second time
    /// either, since nothing below runs when <see cref="TaskOccurrence.Complete"/> itself was
    /// already a no-op (status stays <see cref="TaskOccurrenceStatus.Completed"/>, never
    /// re-entered).
    /// </summary>
    /// <param name="today">
    /// The caller's own "today" (see CLAUDE.md §5 and the <c>/complete</c> endpoint) - decides
    /// whether this completion counts as "tid i förväg" (see below). Never read from a clock
    /// here.
    /// </param>
    public async Task<TaskOccurrence?> HandleAsync(
        Guid householdId,
        Guid occurrenceId,
        Guid completedByMemberId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var occurrence = await _occurrences.FindByIdAsync(householdId, occurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return null;
        }

        var wasAlreadyCompleted = occurrence.Status == TaskOccurrenceStatus.Completed;

        occurrence.Complete(completedByMemberId, _timeProvider.GetUtcNow());

        await _occurrences.UpdateAsync(occurrence, cancellationToken);

        // "Tid i förväg" (docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag
        // och tid i förväg") - earned for doing something before its own due date, OR for an
        // extra task nobody planned for, never both (an extra task is by definition not "ahead
        // of" anything - it was never scheduled at all until now). Skipped entirely when the
        // completion was already a no-op above, so a duplicate tap can never earn credit twice.
        if (!wasAlreadyCompleted)
        {
            if (occurrence.OriginalScheduledDate > today)
            {
                await _credits.AddAsync(
                    MemberTimeCredit.Earned(
                        householdId, completedByMemberId, today, TimeCreditReason.WorkedAhead,
                        occurrence.EstimatedMinutes, occurrence.Id),
                    cancellationToken);
            }
            else if (occurrence.AddedAsExtra)
            {
                await _credits.AddAsync(
                    MemberTimeCredit.Earned(
                        householdId, completedByMemberId, today, TimeCreditReason.ExtraTask,
                        occurrence.EstimatedMinutes, occurrence.Id),
                    cancellationToken);
            }
        }

        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return occurrence;
    }
}
