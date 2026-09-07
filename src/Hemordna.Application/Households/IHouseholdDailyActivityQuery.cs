namespace Hemordna.Application.Households;

/// <summary>
/// How much of the household's shared workload was due, and done, on one calendar day -
/// counted across every member together, never broken down per person. Feeds a household-wide
/// "how did today go" indicator next to the "senaste händelser" log - deliberately never a
/// per-member figure, so it cannot be read as comparing who did more (PRODUCT.md §8: Hemordna
/// does not compare household members with each other).
/// </summary>
public sealed record DailyActivitySummary(DateOnly Date, int CompletedCount, int TotalCount);

public interface IHouseholdDailyActivityQuery
{
    /// <summary>
    /// One entry per calendar day from <paramref name="days"/> days before
    /// <paramref name="today"/> through <paramref name="today"/> inclusive, in that order - a
    /// day with nothing scheduled at all still gets an entry (0, 0), so a caller never has to
    /// special-case a missing day.
    /// </summary>
    Task<IReadOnlyList<DailyActivitySummary>> FindRecentDaysAsync(
        Guid householdId,
        DateOnly today,
        int days,
        CancellationToken cancellationToken);
}
