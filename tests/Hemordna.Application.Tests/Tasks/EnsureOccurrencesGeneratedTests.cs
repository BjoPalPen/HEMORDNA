using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

public class EnsureOccurrencesGeneratedTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-02 is a Monday.
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();

    private EnsureOccurrencesGenerated CreateUseCase()
        => new(_households, _definitions, _occurrences, _assignments, new FixedTimeProvider(Now));

    private async Task<Guid> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return household.Id;
    }

    [Fact]
    public async Task Generates_the_first_occurrence_once_it_is_due()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Equal(Monday, lastDate);
    }

    [Fact]
    public async Task Does_not_generate_ahead_of_when_the_rule_is_due()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Weekly(Monday, DayOfWeek.Friday));
        _definitions.Seed(definition);

        // Today is Monday; the rule is not due until Friday.
        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Null(lastDate);
    }

    [Fact]
    public async Task Catches_up_every_missed_occurrence_up_to_today()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);

        // Three days have passed with nobody opening the app.
        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(3), CancellationToken.None);

        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(householdId, definition.Id, CancellationToken.None);
        Assert.Equal(Monday.AddDays(3), lastDate);
        Assert.Equal(4, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Calling_it_twice_on_the_same_day_does_not_duplicate()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        _definitions.Seed(definition);
        var useCase = CreateUseCase();

        await useCase.HandleAsync(householdId, Monday, CancellationToken.None);
        await useCase.HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Ignores_a_definition_without_a_recurrence_rule()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Engångsstädning", 20, Now);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Ignores_an_inactive_definition_even_with_a_recurrence_rule()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.Deactivate();
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday, CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Generates_an_as_needed_occurrence_once_it_is_stale()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);

        // Never completed: the interval counts from creation (Monday).
        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(14), CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task Does_not_generate_an_as_needed_occurrence_before_it_is_stale()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(13), CancellationToken.None);

        Assert.Equal(0, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task An_as_needed_task_does_not_pile_up_a_second_occurrence_while_one_is_outstanding()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);
        _definitions.Seed(definition);
        var useCase = CreateUseCase();

        await useCase.HandleAsync(householdId, Monday.AddDays(14), CancellationToken.None);
        await useCase.HandleAsync(householdId, Monday.AddDays(20), CancellationToken.None);

        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task An_as_needed_task_becomes_due_again_from_its_last_completion_not_its_creation()
    {
        var householdId = await ArrangeHouseholdAsync();
        var definition = TaskDefinition.Create(householdId, "Putsa fönster", 20, Now);
        definition.SetStaleAfterDays(14);

        // Completed 5 days after creation, so the next due date is 5 + 14 days out - not 14
        // days from creation, which would wrongly ignore the completion.
        var completedAt = Now.AddDays(5);
        var doneOccurrence = definition.ScheduleFor(Monday.AddDays(5), Now);
        doneOccurrence.Complete(Guid.NewGuid(), completedAt);
        _occurrences.Seed(doneOccurrence);
        _definitions.Seed(definition);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(5 + 13), CancellationToken.None);
        Assert.Equal(0, _occurrences.AddCallCount);

        await CreateUseCase().HandleAsync(householdId, Monday.AddDays(5 + 14), CancellationToken.None);
        Assert.Equal(1, _occurrences.AddCallCount);
    }

    [Fact]
    public async Task A_rotating_recurring_task_assigns_and_records_a_turn_for_each_generated_occurrence()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, Now.AddMinutes(1));
        await _households.UpdateAsync(household, CancellationToken.None);

        var definition = TaskDefinition.Create(household.Id, "Diska", 20, Now);
        definition.SetRecurrence(RecurrenceRule.Daily(Monday));
        definition.SetRotatingResponsibility(true);
        definition.SetDefaultResponsibleMember(anna.Id);
        _definitions.Seed(definition);

        // Two days due: rotation should hand Monday to Anna and Tuesday to Bjorn.
        await CreateUseCase().HandleAsync(household.Id, Monday.AddDays(1), CancellationToken.None);

        Assert.Equal(2, _assignments.Count);
        var last = await _assignments.FindMostRecentAsync(household.Id, definition.Id, CancellationToken.None);
        Assert.Equal(bjorn.Id, last!.MemberId);
    }
}
