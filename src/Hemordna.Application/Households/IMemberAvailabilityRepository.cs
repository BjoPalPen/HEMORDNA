using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Reads and writes the one-off availability overrides for a member's day.</summary>
public interface IMemberAvailabilityRepository
{
    /// <summary>The override for this member and date, or <c>null</c> when the weekly budget applies.</summary>
    Task<MemberAvailability?> FindAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task AddAsync(MemberAvailability availability, CancellationToken cancellationToken);

    Task UpdateAsync(MemberAvailability availability, CancellationToken cancellationToken);
}
