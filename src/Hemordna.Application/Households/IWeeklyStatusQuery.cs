namespace Hemordna.Application.Households;

/// <summary>How a member's day looks, for the household overview's weekly dot-matrix (docs/DESIGN.md §6).</summary>
public enum DayStatus
{
    /// <summary>Nothing scheduled for this member on this date.</summary>
    NoPlan,

    /// <summary>At least one occurrence is still outstanding.</summary>
    Planned,

    /// <summary>Everything scheduled for this member on this date is completed.</summary>
    Done
}

public sealed record MemberDayStatus(Guid MemberId, DateOnly Date, DayStatus Status);

/// <summary>
/// Read-only status per member and date, straight from <see cref="Tasks.TaskOccurrence"/> -
/// same reasoning as <see cref="IRecentActivityQuery"/>: no separate event log.
/// </summary>
public interface IWeeklyStatusQuery
{
    /// <summary>Status for every member with at least one occurrence in the 7 days starting at <paramref name="weekStart"/>.</summary>
    Task<IReadOnlyList<MemberDayStatus>> FindWeeklyStatusAsync(
        Guid householdId,
        DateOnly weekStart,
        CancellationToken cancellationToken);
}
