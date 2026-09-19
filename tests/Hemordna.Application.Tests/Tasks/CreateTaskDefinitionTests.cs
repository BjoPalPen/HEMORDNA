using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Common;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class CreateTaskDefinitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // "Altan 1 · Sommarställa, varje månad × 12" - today is 2026-09-19, the rule is anchored on
    // 2027-05-01. See docs/ARCHITECTURE.md "Beslut: Glesa regler lämnas i fred".
    private static readonly DateTimeOffset SparseNow = new(2026, 9, 19, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();

    private CreateTaskDefinition CreateUseCase() => new(_households, _definitions, new FixedTimeProvider(Now));

    private CreateTaskDefinition CreateUseCaseAt(DateTimeOffset now) => new(_households, _definitions, new FixedTimeProvider(now));

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

    /// <summary>"Every Tuesday" locked to Thursday would generate on Tuesdays while planning
    /// treats the task as pinned to Thursday - rejected rather than stored in disagreement.</summary>
    [Fact]
    public async Task Rejects_a_locked_weekday_that_disagrees_with_the_recurrence()
    {
        var householdId = await ArrangeHouseholdAsync();
        var tuesday = new DateOnly(2026, 3, 3);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition(
                "Tömma sopor", 5,
                Recurrence: RecurrenceRule.Weekly(tuesday, DayOfWeek.Tuesday),
                PreferredWeekday: DayOfWeek.Thursday),
            CancellationToken.None));

        Assert.Empty(await _definitions.ListByHouseholdAsync(householdId, CancellationToken.None));
    }

    [Fact]
    public async Task Accepts_a_locked_weekday_that_matches_the_recurrence()
    {
        var householdId = await ArrangeHouseholdAsync();
        var tuesday = new DateOnly(2026, 3, 3);

        var definition = await CreateUseCase().HandleAsync(
            householdId,
            new NewTaskDefinition(
                "Tömma sopor", 5,
                Recurrence: RecurrenceRule.Weekly(tuesday, DayOfWeek.Tuesday),
                PreferredWeekday: DayOfWeek.Tuesday),
            CancellationToken.None);

        Assert.Equal(DayOfWeek.Tuesday, definition!.PreferredWeekday);
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

    /// <summary>
    /// "Altan 1 · Sommarställa, varje månad × 12" created in September must not become a
    /// September task forever - a sparse rule (Interval > 1) carries WHICH month is meant in its
    /// own StartDate, and AutoPlaceWeekday must leave that alone. See docs/ARCHITECTURE.md
    /// "Beslut: Glesa regler lämnas i fred".
    /// </summary>
    [Fact]
    public async Task AutoPlaceWeekday_leaves_a_sparse_monthly_recurrence_untouched()
    {
        var householdId = await ArrangeHouseholdAsync();
        var anchor = new DateOnly(2027, 5, 1);
        var sparse = RecurrenceRule.Monthly(anchor, everyNMonths: 12);

        var definition = await CreateUseCaseAt(SparseNow).HandleAsync(
            householdId,
            new NewTaskDefinition("Sommarställa altanmöblerna", 30, Recurrence: sparse, AutoPlaceWeekday: true),
            CancellationToken.None);

        Assert.Equal(RecurrenceFrequency.Monthly, definition!.Recurrence!.Frequency);
        Assert.Equal(12, definition.Recurrence.Interval);
        Assert.Equal(anchor, definition.Recurrence.StartDate);
        Assert.Null(definition.Recurrence.Weekday);
        Assert.Null(definition.Recurrence.MonthlyWeek);
    }

    /// <summary>Control: the exact same shape but Interval 1 must still be auto-placed as before -
    /// proves the sparse exclusion above did not disable the feature for the ordinary case.</summary>
    [Fact]
    public async Task AutoPlaceWeekday_still_places_an_ordinary_monthly_recurrence()
    {
        var householdId = await ArrangeHouseholdAsync();
        var anchor = new DateOnly(2027, 5, 1);
        var ordinary = RecurrenceRule.Monthly(anchor, everyNMonths: 1);

        var definition = await CreateUseCaseAt(SparseNow).HandleAsync(
            householdId,
            new NewTaskDefinition("Byt vattenfilter", 10, Recurrence: ordinary, AutoPlaceWeekday: true),
            CancellationToken.None);

        Assert.NotNull(definition!.Recurrence!.Weekday);
        Assert.NotNull(definition.Recurrence.MonthlyWeek);
    }
}
