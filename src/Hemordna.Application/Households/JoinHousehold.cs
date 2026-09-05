using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Adds the signing-in user to an existing household via its invite code.</summary>
public sealed class JoinHousehold
{
    private readonly IHouseholdRepository _households;
    private readonly TimeProvider _timeProvider;

    public JoinHousehold(IHouseholdRepository households, TimeProvider timeProvider)
    {
        _households = households;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Adds the user as a new member of the household the code belongs to, or returns
    /// <c>null</c> when no household has that code.
    /// </summary>
    /// <exception cref="Domain.Common.DomainException">
    /// A member with this display name already exists in the household - see
    /// <see cref="Household.AddMember"/>.
    /// </exception>
    public async Task<Household?> HandleAsync(
        string inviteCode,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByInviteCodeAsync(inviteCode.Trim().ToUpperInvariant(), cancellationToken);

        if (household is null)
        {
            return null;
        }

        // Starts with no time allocated, same as the household's first member (CreateHousehold)
        // - the household decides its own budget rather than inheriting a guessed number.
        var member = household.AddMember(displayName, WeeklyTimeBudget.Empty, _timeProvider.GetUtcNow());
        member.LinkToUser(userId);

        await _households.UpdateAsync(household, cancellationToken);

        return household;
    }
}
