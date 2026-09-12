namespace Hemordna.Application.Households;

/// <summary>
/// Which household a signed-in user belongs to, as which member, and whether that member can
/// manage the household - see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad". Carried here,
/// not looked up separately, since every household-scoped request already resolves this once.
/// </summary>
public sealed record HouseholdMembership(Guid HouseholdId, Guid MemberId, bool CanManageHousehold);

/// <summary>
/// Resolves the caller's membership. This is what turns an authenticated user into a
/// household-scoped one - every request that names a household is checked against it.
/// </summary>
public interface IHouseholdMembershipQuery
{
    Task<HouseholdMembership?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken);
}
