using Hemordna.Application.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class SetMemberWeeklyEffortCeilingTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    private SetMemberWeeklyEffortCeiling CreateUseCase() => new(_households);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Replaces_a_new_members_default_ceiling()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        Assert.Equal(WeeklyEffortCeiling.Default, member.WeeklyEffortCeiling);

        var updated = await CreateUseCase().HandleAsync(
            householdId, member.Id, WeeklyEffortCeiling.Uniform(TaskEffort.Light), CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(TaskEffort.Light, updated.WeeklyEffortCeiling.CeilingFor(DayOfWeek.Monday));
        Assert.Equal(TaskEffort.Light, member.WeeklyEffortCeiling.CeilingFor(DayOfWeek.Monday));
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var updated = await CreateUseCase().HandleAsync(
            householdId, Guid.NewGuid(), WeeklyEffortCeiling.Default, CancellationToken.None);

        Assert.Null(updated);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var updated = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), WeeklyEffortCeiling.Default, CancellationToken.None);

        Assert.Null(updated);
    }
}
