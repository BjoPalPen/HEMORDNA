namespace Hemordna.Application.Households;

/// <summary>Which household a signed-in user belongs to, and as which member.</summary>
public sealed record HouseholdMembership(Guid HouseholdId, Guid MemberId);

/// <summary>
/// Resolves the caller's membership. This is what turns an authenticated user into a
/// household-scoped one - every request that names a household is checked against it.
/// </summary>
public interface IHouseholdMembershipQuery
{
    Task<HouseholdMembership?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
