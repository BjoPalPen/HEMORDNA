using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class GetMemberTimeCreditTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 2);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberTimeCreditRepository _credits = new();

    private GetMemberTimeCredit CreateUseCase() => new(_households, _credits);

    private async Task<(Guid HouseholdId, HouseholdMember Anna)> ArrangeHouseholdAsync(int minutesPerDay = 60)
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(minutesPerDay));
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, anna);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_member()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), Today, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_member_with_no_ledger_rows_has_a_balance_of_zero()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Today, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Earned_minutes_within_the_lookback_window_are_summed()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        _credits.Seed(MemberTimeCredit.Earned(householdId, anna.Id, Today, TimeCreditReason.WorkedAhead, 30, Guid.NewGuid()));
        _credits.Seed(MemberTimeCredit.Earned(householdId, anna.Id, Today, TimeCreditReason.ExtraTask, 15, Guid.NewGuid()));

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Today, CancellationToken.None);

        Assert.Equal(45, result);
    }

    [Fact]
    public async Task The_balance_never_reads_as_negative_even_when_consumed_exceeds_earned()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        _credits.Seed(MemberTimeCredit.Earned(householdId, anna.Id, Today, TimeCreditReason.WorkedAhead, 10, Guid.NewGuid()));
        _credits.Seed(MemberTimeCredit.Consumed(householdId, anna.Id, Today, TimeCreditReason.RotationSkipped, 25, Guid.NewGuid()));

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Today, CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task The_balance_is_capped_at_the_members_own_weekly_time_budget()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync(minutesPerDay: 10);
        _credits.Seed(MemberTimeCredit.Earned(householdId, anna.Id, Today, TimeCreditReason.WorkedAhead, 200, Guid.NewGuid()));

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Today, CancellationToken.None);

        Assert.Equal(70, result);
    }

    [Fact]
    public async Task Rows_older_than_60_days_are_ignored()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        _credits.Seed(MemberTimeCredit.Earned(
            householdId, anna.Id, Today.AddDays(-61), TimeCreditReason.WorkedAhead, 40, Guid.NewGuid()));

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Today, CancellationToken.None);

        Assert.Equal(0, result);
    }
}
