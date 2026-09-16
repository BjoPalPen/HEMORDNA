using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class CreateTaskDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private CreateTaskDefinition CreateUseCase() => new(_households, _definitions, new FixedTimeProvider(Now));

    private async Task<Guid> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return household.Id;
    }

    [Fact]
    public async Task Defaults_to_medium_effort_when_none_is_given()
    {
        var householdId = await ArrangeHouseholdAsync();

        var definition = await CreateUseCase().HandleAsync(
            householdId, new NewTaskDefinition("Diska", 20), CancellationToken.None);

        Assert.Equal(TaskEffort.Medium, definition!.Effort);
    }

    [Fact]
    public async Task Applies_the_requested_effort_level()
    {
        var householdId = await ArrangeHouseholdAsync();

        var definition = await CreateUseCase().HandleAsync(
            householdId, new NewTaskDefinition("Skrubba dusch", 15, Effort: TaskEffort.Heavy), CancellationToken.None);

        Assert.Equal(TaskEffort.Heavy, definition!.Effort);
    }
}
