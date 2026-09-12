using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Tasks;

public class CreateExtraTaskTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 2, 3);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private CreateExtraTask CreateUseCase()
        => new(_households, _definitions, _occurrences, _notifier, new FixedTimeProvider(Now));

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Creates_the_definition_and_schedules_it_on_the_caller_today()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var occurrence = await CreateUseCase().HandleAsync(
            householdId, member.Id, new NewExtraTask("Tvätta bilen", 45), Today, CancellationToken.None);

        Assert.NotNull(occurrence);
        Assert.Equal(Today, occurrence.ScheduledDate);
        Assert.Equal(member.Id, occurrence.AssignedMemberId);
        Assert.Equal(45, occurrence.EstimatedMinutes);
        Assert.Equal(1, _occurrences.AddCallCount);

        var definition = Assert.Single(await _definitions.ListByHouseholdAsync(householdId, CancellationToken.None));
        Assert.Equal("Tvätta bilen", definition.Name);
        Assert.Equal(member.Id, definition.DefaultResponsibleMemberId);
        Assert.False(definition.HasRotatingResponsibility);
        Assert.Null(definition.Recurrence);
    }

    [Fact]
    public async Task Notifies_the_household()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        await CreateUseCase().HandleAsync(
            householdId, member.Id, new NewExtraTask("Tvätta bilen", 45), Today, CancellationToken.None);

        Assert.True(_notifier.WasNotified(householdId));
    }

    [Fact]
    public async Task Records_an_optional_description_and_room()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        var garage = household!.AddArea("Garage");
        await _households.UpdateAsync(household, CancellationToken.None);

        var occurrence = await CreateUseCase().HandleAsync(
            householdId, member.Id,
            new NewExtraTask("Tvätta bilen", 45, "Utanpå och innanför.", garage.Id),
            Today, CancellationToken.None);

        var definition = Assert.Single(await _definitions.ListByHouseholdAsync(householdId, CancellationToken.None));
        Assert.Equal("Utanpå och innanför.", definition.Description);
        Assert.Equal(garage.Id, definition.AreaId);
        Assert.Equal(definition.Id, occurrence!.TaskDefinitionId);
    }

    [Fact]
    public async Task Rejects_a_room_from_a_different_household()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        var foreignHousehold = Household.Create("Grannarna", Now);
        var foreignArea = foreignHousehold.AddArea("Garage");

        await Assert.ThrowsAsync<ArgumentException>(() => CreateUseCase().HandleAsync(
            householdId, member.Id, new NewExtraTask("Tvätta bilen", 45, AreaId: foreignArea.Id),
            Today, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var occurrence = await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), new NewExtraTask("Tvätta bilen", 45), Today, CancellationToken.None);

        Assert.Null(occurrence);
    }
}
