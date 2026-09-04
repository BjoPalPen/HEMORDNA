using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// The persistence operations the application actually needs for households. Deliberately
/// not a generic repository: it is named after the use cases it serves and grows only when
/// a use case needs something new.
/// </summary>
public interface IHouseholdRepository
{
    /// <summary>
    /// Persists a new household together with its members and areas. The write is complete
    /// when the task finishes - there is no separate unit of work to commit, because no use
    /// case yet spans more than one aggregate.
    /// </summary>
    Task AddAsync(Household household, CancellationToken cancellationToken);

    /// <summary>Loads a household with its members and areas, or <c>null</c> if it does not exist.</summary>
    Task<Household?> FindByIdAsync(Guid householdId, CancellationToken cancellationToken);
}
