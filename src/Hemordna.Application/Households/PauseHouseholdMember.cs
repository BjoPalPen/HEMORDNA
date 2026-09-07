using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Pauses or resumes a single member's schedule - travelling without the rest of the
/// household. While paused, rotation skips this member (see
/// <see cref="Hemordna.Application.Tasks.RotationPicker"/>) and their own fixed tasks are not
/// newly scheduled either, so nothing piles up on their list while they are away.
/// </summary>
public sealed class PauseHouseholdMember
{
    private readonly IHouseholdRepository _households;

    public PauseHouseholdMember(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Pauses through and including <paramref name="until"/>, or resumes immediately when
    /// <paramref name="until"/> is <c>null</c>. Returns the member, or <c>null</c> when the
    /// household has no such member.
    /// </summary>
    public async Task<HouseholdMember?> HandleAsync(
        Guid householdId, Guid memberId, DateOnly? until, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (household is null || member is null)
        {
            return null;
        }

        if (until is { } date)
        {
            member.Pause(date);
        }
        else
        {
            member.Resume();
        }

        await _households.UpdateAsync(household, cancellationToken);

        return member;
    }
}
