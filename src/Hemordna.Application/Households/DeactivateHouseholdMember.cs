using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Removing someone from a household is a deactivation, not a delete, so completed and
/// scheduled occurrences keep referring to a real person - see HouseholdMember. Rotation
/// already skips inactive members (see RotationPicker), so no other cleanup is needed here.
/// </summary>
public sealed class DeactivateHouseholdMember
{
    private readonly IHouseholdRepository _households;

    public DeactivateHouseholdMember(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Deactivates the member, or returns <c>null</c> when the household has no such member.
    /// </summary>
    /// <exception cref="Domain.Common.DomainException">
    /// This member is the household's last active one who can manage it - see
    /// <see cref="Household.DeactivateMember"/>.
    /// </exception>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null || household.Members.All(m => m.Id != memberId))
        {
            return null;
        }

        var member = household.DeactivateMember(memberId);
        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
