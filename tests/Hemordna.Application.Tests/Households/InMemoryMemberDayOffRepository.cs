using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

internal sealed class InMemoryMemberDayOffRepository : IMemberDayOffRepository
{
    private readonly List<MemberDayOff> _daysOff = [];

    internal int Count => _daysOff.Count;

    internal void Seed(MemberDayOff dayOff) => _daysOff.Add(dayOff);

    public Task<IReadOnlyList<MemberDayOff>> ListForHouseholdAsync(
        Guid householdId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<MemberDayOff>>(_daysOff
            .Where(d => d.HouseholdId == householdId && d.Date >= from && d.Date <= to)
            .ToList());

    public Task<MemberDayOff?> FindAsync(
        Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(_daysOff.FirstOrDefault(d =>
            d.HouseholdId == householdId && d.MemberId == memberId && d.Date == date));

    public Task AddAsync(MemberDayOff dayOff, CancellationToken cancellationToken)
    {
        _daysOff.Add(dayOff);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid householdId, Guid memberId, DateOnly date, CancellationToken cancellationToken)
    {
        _daysOff.RemoveAll(d => d.HouseholdId == householdId && d.MemberId == memberId && d.Date == date);
        return Task.CompletedTask;
    }
}
