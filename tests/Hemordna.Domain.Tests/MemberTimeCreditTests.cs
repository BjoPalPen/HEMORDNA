using Hemordna.Domain.Households;

namespace Hemordna.Domain.Tests;

public class MemberTimeCreditTests
{
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid OccurrenceId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 3, 2);

    [Fact]
    public void Earned_stores_a_positive_amount()
    {
        var entry = MemberTimeCredit.Earned(
            HouseholdId, MemberId, Today, TimeCreditReason.WorkedAhead, 30, OccurrenceId);

        Assert.Equal(30, entry.Minutes);
        Assert.Equal(TimeCreditReason.WorkedAhead, entry.Reason);
    }

    [Fact]
    public void Consumed_stores_the_amount_negated()
    {
        var entry = MemberTimeCredit.Consumed(
            HouseholdId, MemberId, Today, TimeCreditReason.RotationSkipped, 20, OccurrenceId);

        Assert.Equal(-20, entry.Minutes);
        Assert.Equal(TimeCreditReason.RotationSkipped, entry.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Earned_rejects_a_non_positive_amount(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemberTimeCredit.Earned(HouseholdId, MemberId, Today, TimeCreditReason.ExtraTask, minutes, OccurrenceId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Consumed_rejects_a_non_positive_amount(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MemberTimeCredit.Consumed(HouseholdId, MemberId, Today, TimeCreditReason.RotationSkipped, minutes, OccurrenceId));
    }

    [Fact]
    public void BalanceOf_an_empty_ledger_is_zero()
    {
        Assert.Equal(0, MemberTimeCredit.BalanceOf([], cap: 300));
    }

    [Fact]
    public void BalanceOf_sums_earned_and_consumed_rows()
    {
        var entries = new[]
        {
            MemberTimeCredit.Earned(HouseholdId, MemberId, Today, TimeCreditReason.WorkedAhead, 30, OccurrenceId),
            MemberTimeCredit.Earned(HouseholdId, MemberId, Today, TimeCreditReason.ExtraTask, 5, Guid.NewGuid()),
            MemberTimeCredit.Consumed(HouseholdId, MemberId, Today, TimeCreditReason.RotationSkipped, 10, Guid.NewGuid())
        };

        Assert.Equal(25, MemberTimeCredit.BalanceOf(entries, cap: 300));
    }

    [Fact]
    public void BalanceOf_never_goes_negative_even_if_the_ledger_would()
    {
        // Should not happen in practice (nothing consumes more than the balance has), but the
        // floor is a hard guarantee, not an assumption about callers - PRODUCT.md §8: never
        // shown as an "owed" negative number.
        var entries = new[]
        {
            MemberTimeCredit.Earned(HouseholdId, MemberId, Today, TimeCreditReason.WorkedAhead, 5, OccurrenceId),
            MemberTimeCredit.Consumed(HouseholdId, MemberId, Today, TimeCreditReason.RotationSkipped, 20, Guid.NewGuid())
        };

        Assert.Equal(0, MemberTimeCredit.BalanceOf(entries, cap: 300));
    }

    [Fact]
    public void BalanceOf_is_capped()
    {
        var entries = new[]
        {
            MemberTimeCredit.Earned(HouseholdId, MemberId, Today, TimeCreditReason.WorkedAhead, 400, OccurrenceId)
        };

        Assert.Equal(300, MemberTimeCredit.BalanceOf(entries, cap: 300));
    }
}
