using Hemordna.Application.Households;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class ResetHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryMemberTimeCreditRepository _timeCredits = new();

    private ResetHousehold CreateUseCase() => new(_households, _definitions, _timeCredits);

    private async Task<Household> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        household.AddArea("Kök");
        household.AddArea("Badrum");
        household.AddMember("Björn", WeeklyTimeBudget.Uniform(30), Now);
        await _households.UpdateAsync(household, CancellationToken.None);

        return household;
    }

    [Fact]
    public async Task Clears_every_area()
    {
        var household = await ArrangeHouseholdAsync();

        var reset = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotNull(reset);
        Assert.Empty(reset.Areas);
    }

    [Fact]
    public async Task Deletes_every_task_definition_in_the_household()
    {
        var household = await ArrangeHouseholdAsync();
        _definitions.Seed(TaskDefinition.Create(household.Id, "Diska", 10, Now));
        _definitions.Seed(TaskDefinition.Create(household.Id, "Dammsuga", 15, Now));

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        var remaining = await _definitions.ListByHouseholdAsync(household.Id, CancellationToken.None);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task Deletes_every_time_credit_ledger_row_in_the_household()
    {
        var household = await ArrangeHouseholdAsync();
        var member = household.Members.First(m => m.DisplayName == "Björn");
        _timeCredits.Seed(MemberTimeCredit.Earned(
            household.Id, member.Id, DateOnly.FromDateTime(Now.Date), TimeCreditReason.WorkedAhead, 15, Guid.NewGuid()));

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, _timeCredits.Count);
    }

    [Fact]
    public async Task Keeps_every_member()
    {
        var household = await ArrangeHouseholdAsync();

        var reset = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotNull(reset);
        Assert.Equal(2, reset.Members.Count);
        Assert.Contains(reset.Members, m => m.DisplayName == "Anna");
        Assert.Contains(reset.Members, m => m.DisplayName == "Björn");
    }

    [Fact]
    public async Task Keeps_the_households_own_name_and_invite_code()
    {
        var household = await ArrangeHouseholdAsync();
        var inviteCode = household.InviteCode;

        var reset = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotNull(reset);
        Assert.Equal("Familjen", reset.Name);
        Assert.Equal(inviteCode, reset.InviteCode);
    }

    [Fact]
    public async Task Leaves_a_different_households_tasks_alone()
    {
        var household = await ArrangeHouseholdAsync();
        var otherHouseholdId = Guid.NewGuid();
        var otherTask = TaskDefinition.Create(otherHouseholdId, "Tvätta", 20, Now);
        _definitions.Seed(otherTask);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        var remaining = await _definitions.ListByHouseholdAsync(otherHouseholdId, CancellationToken.None);
        Assert.Single(remaining);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var reset = await CreateUseCase().HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(reset);
    }
}
