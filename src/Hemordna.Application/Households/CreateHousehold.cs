using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Creates a new household with the signing-in user as its first member.</summary>
public sealed class CreateHousehold
{
    private readonly IHouseholdRepository _households;
    private readonly TimeProvider _timeProvider;

    public CreateHousehold(IHouseholdRepository households, TimeProvider timeProvider)
    {
        _households = households;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Creates and persists a household, adding the creating user as its first member so the
    /// household is usable immediately. The creation timestamp comes from the injected
    /// <see cref="TimeProvider"/> rather than a static clock, so the use case stays testable.
    /// </summary>
    /// <exception cref="ArgumentException">The household name or display name is blank.</exception>
    public async Task<Household> HandleAsync(
        string name,
        Guid userId,
        string displayName,
        CancellationToken cancellationToken)
    {
        var createdAt = _timeProvider.GetUtcNow();

        var household = Household.Create(name, createdAt);

        // A new member starts with no time allocated: the household decides its own budget
        // rather than inheriting a number Hemordna guessed.
        var member = household.AddMember(displayName, WeeklyTimeBudget.Empty, createdAt);
        member.LinkToUser(userId);

        await _households.AddAsync(household, cancellationToken);

        return household;
    }
}
