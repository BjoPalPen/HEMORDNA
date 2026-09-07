using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class UpdateTaskAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private UpdateTaskAssignment CreateUseCase() => new(_households, _definitions);

    private async Task<Household> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        household.AddMember("Erik", WeeklyTimeBudget.Empty, Now);
        await _households.UpdateAsync(household, CancellationToken.None);

        return household;
    }

    [Fact]
    public async Task Assigning_a_specific_member_turns_off_rotation()
    {
        var household = await ArrangeHouseholdAsync();
        var erik = household.Members.Single(m => m.DisplayName == "Erik");

        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        task.SetRotatingResponsibility(true);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(household.Id, task.Id, erik.Id, CancellationToken.None);

        Assert.Equal(erik.Id, result!.DefaultResponsibleMemberId);
        Assert.False(result.HasRotatingResponsibility);
    }

    [Fact]
    public async Task Clearing_the_member_turns_rotation_back_on()
    {
        var household = await ArrangeHouseholdAsync();
        var erik = household.Members.Single(m => m.DisplayName == "Erik");

        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        task.SetDefaultResponsibleMember(erik.Id);
        task.SetRotatingResponsibility(false);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(household.Id, task.Id, null, CancellationToken.None);

        Assert.Null(result!.DefaultResponsibleMemberId);
        Assert.True(result.HasRotatingResponsibility);
    }

    [Fact]
    public async Task Rejects_a_member_from_another_household()
    {
        var household = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(household.Id, "Diska", 10, Now);
        _definitions.Seed(task);

        var stranger = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateUseCase().HandleAsync(household.Id, task.Id, stranger, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
    {
        var result = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        Assert.Null(result);
    }
}
