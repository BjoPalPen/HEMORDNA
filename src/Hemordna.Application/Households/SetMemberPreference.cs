using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Sets a member's personal presentation and motivation preferences. Individual, not a
/// household setting - see docs/PRODUCT.md §7.
/// </summary>
public sealed class SetMemberPreference
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberPreferenceRepository _preferences;

    public SetMemberPreference(IHouseholdRepository households, IMemberPreferenceRepository preferences)
    {
        _households = households;
        _preferences = preferences;
    }

    /// <summary>
    /// Sets the preference, or returns <c>null</c> when the household has no such member.
    /// Setting it twice updates the existing preference rather than creating a second one.
    /// </summary>
    public async Task<MemberPreference?> HandleAsync(
        Guid householdId,
        Guid memberId,
        PresentationMode presentation,
        MotivationLevel motivation,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        var memberBelongsToHousehold = household is not null
            && household.Members.Any(member => member.Id == memberId);

        if (!memberBelongsToHousehold)
        {
            return null;
        }

        var existing = await _preferences.FindAsync(householdId, memberId, cancellationToken);

        if (existing is not null)
        {
            existing.ChangePresentation(presentation);
            existing.ChangeMotivation(motivation);
            await _preferences.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var preference = MemberPreference.CreateDefault(householdId, memberId);
        preference.ChangePresentation(presentation);
        preference.ChangeMotivation(motivation);
        await _preferences.AddAsync(preference, cancellationToken);

        return preference;
    }
}
