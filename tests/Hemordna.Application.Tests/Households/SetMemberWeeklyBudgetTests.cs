using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class SetMemberWeeklyBudgetTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private SetMemberWeeklyBudget CreateUseCase() => new(_households);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Replaces_a_new_members_empty_budget()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        Assert.Equal(0, member.WeeklyTimeBudget.TotalWeeklyMinutes);

        var updated = await CreateUseCase()
            .HandleAsync(householdId, member.Id, WeeklyTimeBudget.Uniform(30), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(30, updated.WeeklyTimeBudget.MinutesFor(DayOfWeek.Monday));
        Assert.Equal(30, member.WeeklyTimeBudget.MinutesFor(DayOfWeek.Monday));
    }

    [Fact]
    public async Task Replaces_rather_than_merges_an_existing_budget()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        member.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(30));

        var budget = WeeklyTimeBudget.Empty.WithDay(DayOfWeek.Monday, 45);
        var updated = await CreateUseCase()
            .HandleAsync(householdId, member.Id, budget, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(45, updated.WeeklyTimeBudget.MinutesFor(DayOfWeek.Monday));
        Assert.Equal(0, updated.WeeklyTimeBudget.MinutesFor(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var updated = await CreateUseCase()
            .HandleAsync(householdId, Guid.NewGuid(), WeeklyTimeBudget.Uniform(30), CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var updated = await CreateUseCase()
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), WeeklyTimeBudget.Uniform(30), CancellationToken.None);

        Assert.Null(updated);
    }
}
