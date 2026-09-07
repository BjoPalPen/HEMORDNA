using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Pauses or resumes the whole household's schedule - for everyone travelling together, so
/// nothing new piles up while nobody is home to do it. See
/// <see cref="Hemordna.Application.Tasks.EnsureOccurrencesGenerated"/> for what pausing
/// actually skips.
/// </summary>
public sealed class PauseHousehold
{
    private readonly IHouseholdRepository _households;

    public PauseHousehold(IHouseholdRepository households) => _households = households;

    /// <summary>
    /// Pauses through and including <paramref name="until"/>, or resumes immediately when
    /// <paramref name="until"/> is <c>null</c>. Returns the household, or <c>null</c> when it
    /// does not exist.
    /// </summary>
    public async Task<Household?> HandleAsync(
        Guid householdId, DateOnly? until, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return null;
        }

        if (until is { } date)
        {
            household.Pause(date);
        }
        else
        {
            household.Resume();
        }

        await _households.UpdateAsync(household, cancellationToken);

        return household;
    }
}
