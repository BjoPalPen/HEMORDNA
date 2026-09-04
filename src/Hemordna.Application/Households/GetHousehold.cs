using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Reads a single household.</summary>
public sealed class GetHousehold
{
    private readonly IHouseholdRepository _households;

    public GetHousehold(IHouseholdRepository households) => _households = households;

    /// <summary>Returns the household, or <c>null</c> when no household has that id.</summary>
    public Task<Household?> HandleAsync(Guid householdId, CancellationToken cancellationToken)
        => _households.FindByIdAsync(householdId, cancellationToken);
}
