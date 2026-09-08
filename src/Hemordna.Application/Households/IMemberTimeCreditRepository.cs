using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Reads and writes a member's "tid i förväg" ledger.</summary>
public interface IMemberTimeCreditRepository
{
    /// <summary>Every ledger row for this member with <see cref="MemberTimeCredit.OccurredOn"/>
    /// in [<paramref name="from"/>, <paramref name="to"/>].</summary>
    Task<IReadOnlyList<MemberTimeCredit>> ListForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task AddAsync(MemberTimeCredit entry, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every row tied to <paramref name="occurrenceId"/> whose reason is one of
    /// <paramref name="reasons"/> - used when a completion is undone, so credit it earned does
    /// not linger behind. Scoped by reason (rather than "every row for this occurrence") because
    /// a <see cref="TimeCreditReason.RotationSkipped"/> row records what happened to a DIFFERENT
    /// member when this occurrence was generated, not a fact about the occurrence's own
    /// completion - undoing a completion must never touch that row.
    /// </summary>
    Task RemoveByOccurrenceAsync(
        Guid occurrenceId,
        IReadOnlyCollection<TimeCreditReason> reasons,
        CancellationToken cancellationToken);
}
