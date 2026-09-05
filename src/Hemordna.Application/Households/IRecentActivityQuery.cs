namespace Hemordna.Application.Households;

/// <summary>
/// One completed piece of work, for the household overview's "senaste händelser" - built
/// straight from what <see cref="Tasks.TaskOccurrence"/> already records (who completed it,
/// when), not a separate event log. See docs/ARCHITECTURE.md §3 on why a dedicated
/// activity/event entity is not introduced for this.
/// </summary>
public sealed record RecentActivity(
    Guid OccurrenceId,
    string TaskName,
    string MemberDisplayName,
    DateTimeOffset CompletedAt);

public interface IRecentActivityQuery
{
    Task<IReadOnlyList<RecentActivity>> FindRecentlyCompletedAsync(
        Guid householdId,
        int limit,
        CancellationToken cancellationToken);
}
