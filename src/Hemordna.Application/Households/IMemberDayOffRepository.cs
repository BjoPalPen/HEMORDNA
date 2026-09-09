using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>Reads and writes members' individual days off.</summary>
public interface IMemberDayOffRepository
{
    /// <summary>Every day off in the household with a date in [<paramref name="from"/>,
    /// <paramref name="to"/>], across all members - used to render a shared surface (Vecka,
    /// Hushåll) and to keep rotation from picking someone who is off.</summary>
    Task<IReadOnlyList<MemberDayOff>> ListForHouseholdAsync(
        Guid householdId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>This member's day off on this exact date, or <c>null</c> if they are not off
    /// that day.</summary>
    Task<MemberDayOff?> FindAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken);

    Task AddAsync(MemberDayOff dayOff, CancellationToken cancellationToken);

    /// <summary>Removes a day off, if one exists for this member and date. A no-op otherwise -
    /// clearing something already clear is not an error.</summary>
    Task RemoveAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken);
}
