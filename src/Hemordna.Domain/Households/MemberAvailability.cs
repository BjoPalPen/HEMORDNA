using Hemordna.Domain.Common;

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
        int availableMinutes)
    {
        Id = id;
        HouseholdId = householdId;
        MemberId = memberId;
        Date = date;
        AvailableMinutes = availableMinutes;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key. Denormalised so availability can be household-scoped directly.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>Minutes available on <see cref="Date"/>. Zero is valid and means "no time today".</summary>
    public int AvailableMinutes { get; private set; }

    public static MemberAvailability Create(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        Guard.AgainstNegative(availableMinutes, nameof(availableMinutes));

        return new MemberAvailability(Guid.NewGuid(), householdId, memberId, date, availableMinutes);
    }

    /// <summary>Changes the number of minutes available on this date.</summary>
    public void ChangeAvailableMinutes(int availableMinutes)
        => AvailableMinutes = Guard.AgainstNegative(availableMinutes, nameof(availableMinutes));
}
