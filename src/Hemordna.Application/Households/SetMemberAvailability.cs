using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Households;

/// <summary>
/// Records "less time today" or "no time today" for one member and one date, without
/// touching their normal weekly budget. Also carries today's effort ceiling ("Hur är orken
/// idag?" - "Lite") - see <see cref="MemberAvailability.EffortCeiling"/>.
/// </summary>
public sealed class SetMemberAvailability
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberAvailabilityRepository _availabilities;

    public SetMemberAvailability(
        IHouseholdRepository households,
        IMemberAvailabilityRepository availabilities)
    {
        _households = households;
        _availabilities = availabilities;
    }

    /// <summary>
    /// Sets the override, or returns <c>null</c> when the household has no such member.
    /// Setting it twice for the same date updates the existing override rather than
    /// creating a second, contradictory one. <paramref name="effortCeiling"/> is set exactly as
    /// given - a caller that wants to leave an existing ceiling untouched must resend it; left
    /// at its default (<c>null</c>) it clears whatever was there, which is what "Lagom"/"Mycket"
    /// want (see MinDag.razor's <c>SetEnergyAsync</c>).
    /// </summary>
    public async Task<MemberAvailability?> HandleAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        int availableMinutes,
        CancellationToken cancellationToken,
        TaskEffort? effortCeiling = null)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        var memberBelongsToHousehold = household is not null
            && household.Members.Any(member => member.Id == memberId);

        if (!memberBelongsToHousehold)
        {
            return null;
        }

        var existing = await _availabilities.FindAsync(householdId, memberId, date, cancellationToken);

        if (existing is not null)
        {
            existing.ChangeAvailableMinutes(availableMinutes);
            existing.ChangeEffortCeiling(effortCeiling);
            await _availabilities.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var availability = MemberAvailability.Create(householdId, memberId, date, availableMinutes, effortCeiling);
        await _availabilities.AddAsync(availability, cancellationToken);

        return availability;
    }
}
