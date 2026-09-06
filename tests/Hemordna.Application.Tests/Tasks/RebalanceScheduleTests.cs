using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class RebalanceScheduleTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 2, 10);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();

    // DateOnly.ToDateTime returns Kind=Unspecified, which the implicit DateTime->DateTimeOffset
    // conversion treats as LOCAL time - shifting "today" by the machine's UTC offset and
    // silently landing on the wrong calendar date on any non-UTC machine. An explicit
    // TimeSpan.Zero offset pins it to UTC regardless of where this runs.
    private RebalanceSchedule CreateUseCase()
        => new(_households, _definitions, _occurrences,
            new FixedTimeProvider(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)));

    private async Task<Household> SeedHouseholdAsync()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);
        return household;
    }

    private TaskDefinition SeedTask(
        Household household, string name, RecurrenceRule? recurrence, Guid? areaId = null, bool isActive = true)
    {
        var definition = TaskDefinition.Create(household.Id, name, 10, CreatedAt);
        definition.AssignToArea(areaId);
        definition.SetRecurrence(recurrence);

        if (!isActive)
        {
            definition.Deactivate();
        }

        _definitions.Seed(definition);
        return definition;
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var changed = await CreateUseCase().HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(changed);
    }

    [Fact]
    public async Task Two_weekly_tasks_in_different_rooms_end_up_on_different_weekdays()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = Guid.NewGuid();
        var bathroom = Guid.NewGuid();
        var kitchenTask = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), kitchen);
        var bathroomTask = SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), bathroom);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotEqual(kitchenTask.Recurrence!.Weekday, bathroomTask.Recurrence!.Weekday);
    }

    [Fact]
    public async Task Weekly_tasks_in_the_same_room_stay_on_the_same_weekday()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = Guid.NewGuid();
        var wipeCounters = SeedTask(household, "Torka bänken", RecurrenceRule.Weekly(Today, Today.DayOfWeek), kitchen);
        var vacuum = SeedTask(household, "Dammsug golvet", RecurrenceRule.Weekly(Today, Today.DayOfWeek), kitchen);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(wipeCounters.Recurrence!.Weekday, vacuum.Recurrence!.Weekday);
    }

    [Fact]
    public async Task Tasks_with_no_area_are_each_their_own_group()
    {
        var household = await SeedHouseholdAsync();
        var walkDog = SeedTask(household, "Rasta hunden", RecurrenceRule.Weekly(Today, Today.DayOfWeek));
        var buyGroceries = SeedTask(household, "Handla mat", RecurrenceRule.Weekly(Today, Today.DayOfWeek));

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotEqual(walkDog.Recurrence!.Weekday, buyGroceries.Recurrence!.Weekday);
    }

    [Fact]
    public async Task A_daily_tasks_recurrence_is_left_untouched()
    {
        var household = await SeedHouseholdAsync();
        var original = RecurrenceRule.Daily(Today);
        var makeBed = SeedTask(household, "Bädda sängen", original);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(original, makeBed.Recurrence);
    }

    [Fact]
    public async Task An_as_needed_tasks_with_no_recurrence_is_ignored()
    {
        var household = await SeedHouseholdAsync();
        var dustShelves = TaskDefinition.Create(household.Id, "Damma hyllor", 5, CreatedAt);
        dustShelves.SetStaleAfterDays(21);
        _definitions.Seed(dustShelves);

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, changed);
    }

    [Fact]
    public async Task An_inactive_tasks_recurrence_is_left_untouched()
    {
        var household = await SeedHouseholdAsync();
        var original = RecurrenceRule.Weekly(Today, Today.DayOfWeek);
        var removedTask = SeedTask(household, "Borttagen uppgift", original, isActive: false);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(original, removedTask.Recurrence);
    }

    [Fact]
    public async Task Monthly_tasks_in_different_groups_land_on_different_days_of_the_month()
    {
        var household = await SeedHouseholdAsync();
        var trimWipe = SeedTask(household, "Torka lister", RecurrenceRule.Monthly(Today), Guid.NewGuid());
        var windowWash = SeedTask(household, "Tvätta fönster", RecurrenceRule.Monthly(Today), Guid.NewGuid());

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.NotEqual(trimWipe.Recurrence!.StartDate, windowWash.Recurrence!.StartDate);
    }

    [Fact]
    public async Task A_daily_task_never_counts_as_changed_even_alongside_a_weekly_one_that_does()
    {
        // Which of the two groups lands on "index 0" (and so keeps today's weekday
        // unchanged) is not something a caller should have to predict - assert only what is
        // guaranteed regardless: the daily task is never touched, and the total change count
        // never exceeds "every group but the one that happened to land on today".
        var household = await SeedHouseholdAsync();
        SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Bädda sängen", RecurrenceRule.Daily(Today), Guid.NewGuid());

        var changed = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.InRange(changed!.Value, 0, 1);
    }

    [Fact]
    public async Task Three_weekly_rooms_on_the_same_weekday_end_up_on_three_different_weekdays()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        var bathroom = SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        var hallway = SeedTask(household, "Dammsug hallen", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        var weekdays = new[] { kitchen.Recurrence!.Weekday, bathroom.Recurrence!.Weekday, hallway.Recurrence!.Weekday };
        Assert.Equal(3, weekdays.Distinct().Count());
    }

    [Fact]
    public async Task Running_it_twice_in_a_row_changes_nothing_the_second_time()
    {
        var household = await SeedHouseholdAsync();
        SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);
        var secondRunChanged = await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(0, secondRunChanged);
    }

    [Fact]
    public async Task An_already_generated_outstanding_occurrence_due_today_moves_with_its_definition()
    {
        // The scenario this exists for: a household set up before this feature existed (or
        // before it was last run) already has a first occurrence generated under the old,
        // clustered anchor - rebalancing the definition's future recurrence alone would never
        // fix what is already sitting on someone's day right now.
        var household = await SeedHouseholdAsync();
        var kitchen = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        var occurrence = kitchen.ScheduleFor(Today, CreatedAt);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(kitchen.Recurrence!.StartDate, occurrence.ScheduledDate);
    }

    [Fact]
    public async Task An_occurrence_already_scheduled_in_the_future_is_left_alone()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        var farFuture = Today.AddDays(30);
        var occurrence = kitchen.ScheduleFor(farFuture, CreatedAt);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(farFuture, occurrence.ScheduledDate);
    }

    [Fact]
    public async Task A_completed_occurrence_is_left_alone()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        var occurrence = kitchen.ScheduleFor(Today, CreatedAt);
        occurrence.Complete(Guid.NewGuid(), CreatedAt);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(Today, occurrence.ScheduledDate);
    }

    [Fact]
    public async Task An_occurrence_that_cannot_be_deferred_is_left_alone()
    {
        var household = await SeedHouseholdAsync();
        var kitchen = SeedTask(household, "Diska", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        SeedTask(household, "Skrubba badkar", RecurrenceRule.Weekly(Today, Today.DayOfWeek), Guid.NewGuid());
        kitchen.SetCanBeDeferred(false);
        var occurrence = kitchen.ScheduleFor(Today, CreatedAt);
        _occurrences.Seed(occurrence);

        await CreateUseCase().HandleAsync(household.Id, CancellationToken.None);

        Assert.Equal(Today, occurrence.ScheduledDate);
    }
}
