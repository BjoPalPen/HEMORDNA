using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class ScheduleTaskOccurrenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 2, 2);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private ScheduleTaskOccurrence CreateUseCase()
        => new(_households, _definitions, _occurrences, _assignments, _notifier, new FixedTimeProvider(Now));

    private async Task<(Guid HouseholdId, HouseholdMember Anna, HouseholdMember Bjorn, HouseholdMember Cecilia)>
        ArrangeThreeMemberHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();

        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now.AddMinutes(1));
        var cecilia = household.AddMember("Cecilia", WeeklyTimeBudget.Empty, Now.AddMinutes(2));
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, anna, bjorn, cecilia);
    }

    private TaskDefinition SeedRotatingDefinition(Guid householdId, Guid? defaultResponsibleMemberId = null)
    {
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRotatingResponsibility(true);
        definition.SetDefaultResponsibleMember(defaultResponsibleMemberId);
        _definitions.Seed(definition);
        return definition;
    }

    [Fact]
    public async Task First_rotation_picks_the_default_responsible_member()
    {
        var (householdId, anna, _, _) = await ArrangeThreeMemberHouseholdAsync();
        var definition = SeedRotatingDefinition(householdId, anna.Id);

        var occurrence = await CreateUseCase()
            .HandleAsync(householdId, definition.Id, Monday, assignToMemberId: null, CancellationToken.None);

        Assert.NotNull(occurrence);
        Assert.Equal(anna.Id, occurrence.AssignedMemberId);
        Assert.Equal(1, _assignments.Count);
    }

    [Fact]
    public async Task First_rotation_without_a_default_picks_the_earliest_joined_member()
    {
        var (householdId, anna, _, _) = await ArrangeThreeMemberHouseholdAsync();
        var definition = SeedRotatingDefinition(householdId);

        var occurrence = await CreateUseCase()
            .HandleAsync(householdId, definition.Id, Monday, assignToMemberId: null, CancellationToken.None);

        Assert.Equal(anna.Id, occurrence!.AssignedMemberId);
    }

    [Fact]
    public async Task Rotation_advances_to_the_next_member_each_time()
    {
        var (householdId, anna, bjorn, cecilia) = await ArrangeThreeMemberHouseholdAsync();
        var definition = SeedRotatingDefinition(householdId, anna.Id);
        var useCase = CreateUseCase();

        var first = await useCase.HandleAsync(householdId, definition.Id, Monday, null, CancellationToken.None);
        var second = await useCase.HandleAsync(householdId, definition.Id, Monday.AddDays(1), null, CancellationToken.None);
        var third = await useCase.HandleAsync(householdId, definition.Id, Monday.AddDays(2), null, CancellationToken.None);
        var fourth = await useCase.HandleAsync(householdId, definition.Id, Monday.AddDays(3), null, CancellationToken.None);

        Assert.Equal(anna.Id, first!.AssignedMemberId);
        Assert.Equal(bjorn.Id, second!.AssignedMemberId);
        Assert.Equal(cecilia.Id, third!.AssignedMemberId);
        // Cycles back to the start once everyone has had a turn.
        Assert.Equal(anna.Id, fourth!.AssignedMemberId);
    }

    [Fact]
    public async Task An_explicit_assignment_on_a_rotating_task_still_records_a_turn()
    {
        var (householdId, anna, bjorn, cecilia) = await ArrangeThreeMemberHouseholdAsync();
        var definition = SeedRotatingDefinition(householdId, anna.Id);
        var useCase = CreateUseCase();

        // The household manually hands the first turn to Cecilia instead of following the default.
        var overridden = await useCase.HandleAsync(householdId, definition.Id, Monday, cecilia.Id, CancellationToken.None);
        var next = await useCase.HandleAsync(householdId, definition.Id, Monday.AddDays(1), null, CancellationToken.None);

        Assert.Equal(cecilia.Id, overridden!.AssignedMemberId);
        // Rotation continues from Cecilia, wrapping back to Anna, not from the never-used default.
        Assert.Equal(anna.Id, next!.AssignedMemberId);
    }

    [Fact]
    public async Task A_non_rotating_task_never_records_an_assignment_turn()
    {
        var (householdId, anna, _, _) = await ArrangeThreeMemberHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        _definitions.Seed(definition);

        var occurrence = await CreateUseCase()
            .HandleAsync(householdId, definition.Id, Monday, anna.Id, CancellationToken.None);

        Assert.Equal(anna.Id, occurrence!.AssignedMemberId);
        Assert.Equal(0, _assignments.Count);
    }

    [Fact]
    public async Task Scheduling_notifies_the_household()
    {
        var (householdId, anna, _, _) = await ArrangeThreeMemberHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, definition.Id, Monday, anna.Id, CancellationToken.None);

        Assert.True(_notifier.WasNotified(householdId));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_definition()
    {
        var occurrence = await CreateUseCase()
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Monday, null, CancellationToken.None);

        Assert.Null(occurrence);
    }
}
