using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>Why a <see cref="MemberTimeCredit"/> row exists.</summary>
public enum TimeCreditReason
{
    /// <summary>Completed a task before its own due date - "jobba i förväg".</summary>
    WorkedAhead,

    /// <summary>Completed a task added through the "Extra uppgift" flow.</summary>
    ExtraTask,

    /// <summary>Rotation skipped this member for a new occurrence because they had credit to
    /// spend, giving it to someone else instead.</summary>
    RotationSkipped
}

/// <summary>
/// One row in a member's "tid i förväg" ledger - never a single mutable balance field. A
/// ledger keeps every earn and spend as its own fact, so the running total
/// (<see cref="BalanceOf"/>) is always a pure sum of what actually happened, the same
/// snapshot-not-derived-state reasoning <see cref="Tasks.TaskAssignment"/> already uses for the
/// rotation ratio. <see cref="Minutes"/> is positive for an earned row, negative for a consumed
/// one - a single signed column rather than a separate "kind of amount" field, since the sign
/// alone already says which one it is.
/// </summary>
/// <remarks>
/// This is deliberately NOT visible to anyone but the member it belongs to, and deliberately
/// just one number and one sentence in the UI (see docs/ARCHITECTURE.md "Beslut: Kvarlämnat,
/// Imorgon på Idag, ledig dag och tid i förväg") - no history, no chart, no streak, nothing
/// CLAUDE.md §12 would call gamification. The ledger shape exists for correctness (an auditable
/// running total, and a way to undo one event - see <c>ReopenTaskOccurrence</c> - without
/// guessing what to subtract), not to be shown as a history.
/// </remarks>
public sealed class MemberTimeCredit
{
    private MemberTimeCredit(
        Guid id, Guid householdId, Guid memberId, DateOnly occurredOn, int minutes, TimeCreditReason reason,
        Guid occurrenceId)
    {
        Id = id;
        HouseholdId = householdId;
        MemberId = memberId;
        OccurredOn = occurredOn;
        Minutes = minutes;
        Reason = reason;
        OccurrenceId = occurrenceId;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly OccurredOn { get; private set; }

    /// <summary>Positive for an earned row, negative for a consumed one.</summary>
    public int Minutes { get; private set; }

    public TimeCreditReason Reason { get; private set; }

    /// <summary>The occurrence that caused this row - lets a specific row be found and removed
    /// again (e.g. undoing a completion) without having to reconstruct what should be undone.</summary>
    public Guid OccurrenceId { get; private set; }

    /// <summary>Records credit earned - completing something ahead of when it was due, or an
    /// extra task nobody planned for.</summary>
    public static MemberTimeCredit Earned(
        Guid householdId, Guid memberId, DateOnly occurredOn, TimeCreditReason reason, int minutes, Guid occurrenceId)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        Guard.AgainstEmpty(occurrenceId, nameof(occurrenceId));
        Guard.AgainstNonPositive(minutes, nameof(minutes));

        return new MemberTimeCredit(Guid.NewGuid(), householdId, memberId, occurredOn, minutes, reason, occurrenceId);
    }

    /// <summary>Records credit spent - rotation handed a new occurrence to someone else because
    /// this member already had credit to draw down. <paramref name="minutes"/> is the positive
    /// amount consumed; it is stored negated.</summary>
    public static MemberTimeCredit Consumed(
        Guid householdId, Guid memberId, DateOnly occurredOn, TimeCreditReason reason, int minutes, Guid occurrenceId)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        Guard.AgainstEmpty(occurrenceId, nameof(occurrenceId));
        Guard.AgainstNonPositive(minutes, nameof(minutes));

        return new MemberTimeCredit(Guid.NewGuid(), householdId, memberId, occurredOn, -minutes, reason, occurrenceId);
    }

    /// <summary>
    /// The member's current balance: the ledger's own sum, clamped to [0, <paramref name="cap"/>].
    /// The floor means a member is never shown as "owing" time - PRODUCT.md §8 forbids exactly
    /// that ("röda siffror för obetalda skulder i tid"). The cap (the member's own
    /// <see cref="WeeklyTimeBudget.TotalWeeklyMinutes"/>, applied by the caller) keeps a long
    /// unspent streak from reading as a target to keep chasing rather than a simple, bounded
    /// fact.
    /// </summary>
    public static int BalanceOf(IEnumerable<MemberTimeCredit> entries, int cap)
        => Math.Clamp(entries.Sum(entry => entry.Minutes), 0, cap);
}
