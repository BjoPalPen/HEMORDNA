using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
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

    private async Task<(Guid HouseholdId, Guid AreaId)> ArrangeHouseholdWithAreaAsync(string areaName)
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var area = household.AddArea(areaName);
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, area.Id);
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

    [Fact]
    public async Task AutoPlaceWeekday_ignores_the_clients_own_anchor_and_chooses_one_itself()
    {
        var householdId = await ArrangeHouseholdAsync();
        // A placeholder anchor the server must ignore and replace - every weekday is equally
        // empty, so the algorithm's own tie-break (Monday first) decides, not this Friday.
        var placeholderAnchor = RecurrenceRule.Weekly(new DateOnly(2026, 2, 6), DayOfWeek.Friday);

        var definition = await CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition("Dammsug golvet", 15, Recurrence: placeholderAnchor, AutoPlaceWeekday: true),
            CancellationToken.None);

        Assert.Equal(DayOfWeek.Monday, definition!.Recurrence!.Weekday);
    }

    [Fact]
    public async Task AutoPlaceWeekday_joins_an_existing_visit_in_the_same_room_and_kind()
    {
        var (householdId, area) = await ArrangeHouseholdWithAreaAsync("Badrum");

        var existing = TaskDefinition.Create(householdId, "Dammsug golvet", 10, Now);
        existing.AssignToArea(area);
        existing.SetRecurrence(RecurrenceRule.Weekly(new DateOnly(2026, 2, 6), DayOfWeek.Thursday));
        _definitions.Seed(existing);

        var placeholderAnchor = RecurrenceRule.Weekly(new DateOnly(2026, 2, 6), DayOfWeek.Monday);

        var definition = await CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition("Torka golvet", 5, AreaId: area, Recurrence: placeholderAnchor, AutoPlaceWeekday: true),
            CancellationToken.None);

        // Samma besök (rum + Medium/RegularClean), samma dag som den redan där varande uppgiften
        // - inte en fristående placering.
        Assert.Equal(DayOfWeek.Thursday, definition!.Recurrence!.Weekday);
    }

    [Fact]
    public async Task AutoPlaceWeekday_never_moves_an_already_placed_task()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var kitchen = household.AddArea("Kök");
        var bathroom = household.AddArea("Badrum");
        await _households.UpdateAsync(household, CancellationToken.None);
        var householdId = household.Id;

        var existing = TaskDefinition.Create(householdId, "Diska köket", 90, Now);
        existing.AssignToArea(kitchen.Id);
        existing.SetRecurrence(RecurrenceRule.Weekly(new DateOnly(2026, 2, 6), DayOfWeek.Monday));
        _definitions.Seed(existing);

        await CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition(
                "Skrubba badkaret", 20, AreaId: bathroom.Id,
                Recurrence: RecurrenceRule.Weekly(new DateOnly(2026, 2, 6), DayOfWeek.Monday),
                AutoPlaceWeekday: true),
            CancellationToken.None);

        var reloadedExisting = await _definitions.FindByIdAsync(householdId, existing.Id, CancellationToken.None);
        Assert.Equal(DayOfWeek.Monday, reloadedExisting!.Recurrence!.Weekday);
    }

    [Fact]
    public async Task AutoPlaceWeekday_places_a_monthly_task_on_a_weekday_with_a_default_week_of_month()
    {
        var householdId = await ArrangeHouseholdAsync();
        var placeholderAnchor = RecurrenceRule.Monthly(new DateOnly(2026, 2, 6));

        var definition = await CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition("Torka lister", 10, Recurrence: placeholderAnchor, AutoPlaceWeekday: true),
            CancellationToken.None);

        Assert.NotNull(definition!.Recurrence!.Weekday);
        Assert.Equal(WeekOfMonth.First, definition.Recurrence.MonthlyWeek);
    }
}
