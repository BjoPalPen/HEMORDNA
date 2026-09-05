using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Reads a member's presentation and motivation preferences.</summary>
public sealed class GetMemberPreference
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberPreferenceRepository _preferences;

    public GetMemberPreference(IHouseholdRepository households, IMemberPreferenceRepository preferences)
    {
        _households = households;
        _preferences = preferences;
    }

    /// <summary>
    /// The member's saved preference, or the defaults if they have never set one - the
    /// household's own choice not to override the default is not distinguishable from "not
    /// asked yet", and does not need to be. Returns <c>null</c> only when the household has
    /// no such member.
    /// </summary>
    public async Task<MemberPreference?> HandleAsync(
        Guid householdId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        var memberBelongsToHousehold = household is not null
            && household.Members.Any(member => member.Id == memberId);

        if (!memberBelongsToHousehold)
        {
            return null;
        }

        return await _preferences.FindAsync(householdId, memberId, cancellationToken)
            ?? MemberPreference.CreateDefault(householdId, memberId);
    }
}
