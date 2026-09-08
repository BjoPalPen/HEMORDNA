using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// A member's own, deliberate "day off" - a single date they are excluded from rotation on,
/// exactly like a household-wide or per-member pause (<see cref="HouseholdMember.PausedUntil"/>)
/// but for one specific day rather than an open-ended range. Nothing is scheduled TO this date
/// for the member (see <see cref="Tasks.RotationPicker"/>'s fallback, which must never choose
/// someone who is off); anything the member already has here is theirs to bring forward or
/// leave for later, never silently reassigned.
/// </summary>
/// <remarks>
/// A day off is deliberately its own entity rather than, say, a
/// <see cref="MemberAvailability"/> override of zero minutes: availability-zero still lets the
/// day be picked (a task can still be assigned there and simply not fit, going to
/// <c>Unplanned</c>), whereas a day off is a HARD exclusion from rotation entirely - the two
/// mean different things to <see cref="Tasks.RotationPicker"/> and must not be conflated.
/// </remarks>
public sealed class MemberDayOff
{
    private MemberDayOff(Guid householdId, Guid memberId, DateOnly date)
    {
        HouseholdId = householdId;
        MemberId = memberId;
        Date = date;
    }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    public Guid MemberId { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>
    /// Creates a day off for <paramref name="date"/>, which must be within the next 7 days
    /// (inclusive) of <paramref name="today"/> - never in the past, and never further out than
    /// that. 7 days matches how far ahead Vecka itself shows: a day off is a near-term,
    /// this-week decision, made close to the day it applies to. Something further out is a
    /// longer-term absence and belongs to Pausa instead, which already covers open-ended ranges
    /// without needing one row per date.
    /// </summary>
    public static MemberDayOff Create(Guid householdId, Guid memberId, DateOnly date, DateOnly today)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));

        if (date < today)
        {
            throw new DomainException("A day off cannot be set for a date that has already passed.");
        }

        if (date > today.AddDays(7))
        {
            throw new DomainException("A day off can only be set up to 7 days ahead - use Pausa for longer absences.");
        }

        return new MemberDayOff(householdId, memberId, date);
    }
}
