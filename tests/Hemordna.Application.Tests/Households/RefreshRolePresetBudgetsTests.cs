using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class RefreshRolePresetBudgetsTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private static readonly WeeklyTimeBudget OldAdultFullTime = WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int>
    {
        [DayOfWeek.Monday] = 30,
        [DayOfWeek.Tuesday] = 30,
        [DayOfWeek.Wednesday] = 30,
        [DayOfWeek.Thursday] = 30,
        [DayOfWeek.Friday] = 30,
        [DayOfWeek.Saturday] = 60,
        [DayOfWeek.Sunday] = 60
    });

    private static readonly WeeklyTimeBudget OldRetired = WeeklyTimeBudget.Uniform(60);

    private readonly InMemoryHouseholdRepository _households = new();

    private RefreshRolePresetBudgets CreateUseCase() => new(_households);

    [Fact]
    public async Task Refreshes_a_member_whose_budget_matches_the_old_adult_full_time_preset()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Helena", OldAdultFullTime, CreatedAt, HouseholdRole.AdultFullTime);
        await _households.AddAsync(household, CancellationToken.None);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(1, changed);
        Assert.Equal(WeeklyTimeBudget.Uniform(35), member.WeeklyTimeBudget);
    }

    [Fact]
    public async Task Refreshes_a_member_whose_budget_matches_the_old_retired_preset()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Björn", OldRetired, CreatedAt, HouseholdRole.Retired);
        await _households.AddAsync(household, CancellationToken.None);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(1, changed);
        Assert.Equal(WeeklyTimeBudget.Uniform(65), member.WeeklyTimeBudget);
    }

    [Fact]
    public async Task Does_not_touch_a_hand_customized_budget_even_with_a_matching_role()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var custom = WeeklyTimeBudget.Uniform(45); // deliberately does not match the old preset
        var member = household.AddMember("Helena", custom, CreatedAt, HouseholdRole.AdultFullTime);
        await _households.AddAsync(household, CancellationToken.None);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, changed);
        Assert.Equal(custom, member.WeeklyTimeBudget);
    }

    [Fact]
    public async Task Does_not_touch_a_member_with_no_role()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Gäst", OldAdultFullTime, CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, changed);
        Assert.Equal(OldAdultFullTime, member.WeeklyTimeBudget);
    }

    [Fact]
    public async Task Does_not_touch_a_child_or_teen_member()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var childBudget = WeeklyTimeBudget.Create(new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = 15, [DayOfWeek.Tuesday] = 15, [DayOfWeek.Wednesday] = 15,
            [DayOfWeek.Thursday] = 15, [DayOfWeek.Friday] = 15, [DayOfWeek.Saturday] = 30, [DayOfWeek.Sunday] = 30
        });
        var member = household.AddMember("Charlie", childBudget, CreatedAt, HouseholdRole.ChildOrTeen);
        await _households.AddAsync(household, CancellationToken.None);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, changed);
        Assert.Equal(childBudget, member.WeeklyTimeBudget);
    }

    [Fact]
    public async Task Rerunning_immediately_changes_nothing()
    {
        var household = Household.Create("Familjen", CreatedAt);
        household.AddMember("Helena", OldAdultFullTime, CreatedAt, HouseholdRole.AdultFullTime);
        household.AddMember("Björn", OldRetired, CreatedAt.AddMinutes(1), HouseholdRole.Retired);
        await _households.AddAsync(household, CancellationToken.None);

        var useCase = CreateUseCase();
        var firstRun = await useCase.HandleAsync(household.Id, CancellationToken.None);
        var secondRun = await useCase.HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(2, firstRun);
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var changed = await CreateUseCase().HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(changed);
    }
}
