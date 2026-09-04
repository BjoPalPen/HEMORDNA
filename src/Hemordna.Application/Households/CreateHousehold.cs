using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Creates a new household.</summary>
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
    /// Creates and persists a household. The creation timestamp comes from the injected
    /// <see cref="TimeProvider"/> rather than a static clock, so the use case stays testable.
    /// </summary>
    /// <exception cref="ArgumentException">The name is null or whitespace.</exception>
    public async Task<Household> HandleAsync(string name, CancellationToken cancellationToken)
    {
        var household = Household.Create(name, _timeProvider.GetUtcNow());

        await _households.AddAsync(household, cancellationToken);

        return household;
    }
}
