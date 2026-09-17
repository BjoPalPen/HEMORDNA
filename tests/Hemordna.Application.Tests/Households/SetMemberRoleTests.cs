using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class SetMemberRoleTests
{
    private readonly InMemoryHouseholdRepository _households = new();

    private async Task<Household> CreateAsync()
        => await new CreateHousehold(_households, new FixedTimeProvider(
            new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero)))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

    [Fact]
    public async Task Saves_role_budget_and_effort_together()
    {
        var household = await CreateAsync();
        var member = household.Members.Single();
        await new SetMemberRole(_households).HandleAsync(household.Id, member.Id,
            HouseholdRole.Retired, CancellationToken.None,
            WeeklyTimeBudget.Uniform(65), WeeklyEffortCeiling.Uniform(TaskEffort.Light));

        Assert.Equal(HouseholdRole.Retired, member.Role);
        Assert.Equal(65, member.WeeklyTimeBudget.MinutesFor(DayOfWeek.Monday));
        Assert.Equal(TaskEffort.Light, member.WeeklyEffortCeiling.CeilingFor(DayOfWeek.Monday));
        Assert.Equal(1, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Role_only_update_preserves_custom_capacity()
    {
        var household = await CreateAsync();
        var member = household.Members.Single();
        member.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(42));
        member.ChangeWeeklyEffortCeiling(WeeklyEffortCeiling.Uniform(TaskEffort.Light));
        await new SetMemberRole(_households).HandleAsync(household.Id, member.Id,
            HouseholdRole.Retired, CancellationToken.None);

        Assert.Equal(42, member.WeeklyTimeBudget.MinutesFor(DayOfWeek.Monday));
        Assert.Equal(TaskEffort.Light, member.WeeklyEffortCeiling.CeilingFor(DayOfWeek.Monday));
    }

    [Fact]
    public async Task Incomplete_preset_does_not_change_the_member()
    {
        var household = await CreateAsync();
        var member = household.Members.Single();
        var originalRole = member.Role;
        await Assert.ThrowsAsync<ArgumentException>(() => new SetMemberRole(_households)
            .HandleAsync(household.Id, member.Id, HouseholdRole.Retired,
                CancellationToken.None, WeeklyTimeBudget.Uniform(65)));

        Assert.Equal(originalRole, member.Role);
        Assert.Equal(0, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Cannot_update_a_member_in_another_household()
    {
        var household = await CreateAsync();
        var result = await new SetMemberRole(_households).HandleAsync(
            Guid.NewGuid(), household.Members.Single().Id, HouseholdRole.Retired,
            CancellationToken.None, WeeklyTimeBudget.Uniform(65), WeeklyEffortCeiling.Uniform(TaskEffort.Light));

        Assert.Null(result);
        Assert.Equal(0, _households.UpdateCallCount);
    }
}
