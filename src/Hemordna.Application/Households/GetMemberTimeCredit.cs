using Hemordna.Domain.Households;

namespace Hemordna.Application.Households;

/// <summary>
/// Reads a member's own "tid i förväg" balance - "Det jag gör i förväg märks, och jag kan se
/// hur" (see docs/ARCHITECTURE.md "Beslut: Kvarlämnat, Imorgon på Idag, ledig dag och tid i
/// förväg", Del C). Deliberately the only way to see this number: one member, one balance, no
/// history endpoint - see <see cref="MemberTimeCredit"/>.
/// </summary>
public sealed class GetMemberTimeCredit
{
    /// <summary>Matches the lookback <see cref="Tasks.EnsureOccurrencesGenerated"/> already uses
    /// for the same ledger, so the number shown here is always the same one rotation itself is
    /// acting on.</summary>
    private const int LookbackDays = 60;

    private readonly IHouseholdRepository _households;
    private readonly IMemberTimeCreditRepository _credits;

    public GetMemberTimeCredit(IHouseholdRepository households, IMemberTimeCreditRepository credits)
    {
        _households = households;
        _credits = credits;
    }

    /// <summary>The member's current balance in minutes, or <c>null</c> when the household has no
    /// such member.</summary>
    public async Task<int?> HandleAsync(
        Guid householdId, Guid memberId, DateOnly today, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (member is null)
        {
            return null;
        }

        var entries = await _credits.ListForMemberAsync(
            householdId, memberId, today.AddDays(-LookbackDays), today, cancellationToken);

        return MemberTimeCredit.BalanceOf(entries, member.WeeklyTimeBudget.TotalWeeklyMinutes);
    }
}
