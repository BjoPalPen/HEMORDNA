using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Households;

/// <summary>
/// A deliberate override of a member's normal weekly time budget for one specific date
/// ("less time today", "no time today"). The absence of an override means the weekly
/// budget applies; the weekly budget itself is never mutated by a one-off change.
/// </summary>
public sealed class MemberAvailability
{
    private MemberAvailability(
        Guid id,
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes,
        TaskEffort? effortCeiling)
    {
        Id = id;
        HouseholdId = householdId;
        MemberId = memberId;
        Date = date;
        AvailableMinutes = availableMinutes;
        EffortCeiling = effortCeiling;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key. Denormalised so availability can be household-scoped directly.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>Minutes available on <see cref="Date"/>. Zero is valid and means "no time today".</summary>
    public int AvailableMinutes { get; private set; }

    /// <summary>
    /// "Hur är orken idag?" - "Lite" ("orkvalet styr dagens tyngd", see docs/ARCHITECTURE.md).
    /// The heaviest <see cref="TaskEffort"/> the daily planner may still plan for this member on
    /// <see cref="Date"/> - <c>null</c> means no ceiling at all ("Lagom"/"Mycket", or no choice
    /// made yet). Independent of <see cref="AvailableMinutes"/>: one is how much time there is,
    /// the other is how heavy that time may be spent.
    /// </summary>
    public TaskEffort? EffortCeiling { get; private set; }

    public static MemberAvailability Create(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes,
        TaskEffort? effortCeiling = null)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        Guard.AgainstNegative(availableMinutes, nameof(availableMinutes));
        RequireValidEffort(effortCeiling);

        return new MemberAvailability(Guid.NewGuid(), householdId, memberId, date, availableMinutes, effortCeiling);
    }

    /// <summary>Changes the number of minutes available on this date.</summary>
    public void ChangeAvailableMinutes(int availableMinutes)
        => AvailableMinutes = Guard.AgainstNegative(availableMinutes, nameof(availableMinutes));

    /// <summary>Changes today's effort ceiling, or clears it with <c>null</c> ("Lagom"/"Mycket" -
    /// no tyngdfilter).</summary>
    public void ChangeEffortCeiling(TaskEffort? effortCeiling)
    {
        RequireValidEffort(effortCeiling);
        EffortCeiling = effortCeiling;
    }

    private static void RequireValidEffort(TaskEffort? effort)
    {
        if (effort is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(effort), value, "Not a valid effort level.");
        }
    }
}
