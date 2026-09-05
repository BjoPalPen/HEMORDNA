using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>One member's personal display preferences.</summary>
public interface IMemberPreferenceRepository
{
    Task<MemberPreference?> FindAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken);

    Task AddAsync(MemberPreference preference, CancellationToken cancellationToken);

    Task UpdateAsync(MemberPreference preference, CancellationToken cancellationToken);
}
