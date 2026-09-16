using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Planning;

public class ApplyWeeklyPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-04 is a Wednesday; 2026-03-02 is the Monday of that same week;
    // 2026-02-27 is the Friday of the PREVIOUS week.
    private static readonly DateOnly Wednesday = new(2026, 3, 4);
    private static readonly DateOnly OldFriday = new(2026, 2, 27);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly InMemoryMemberTimeCreditRepository _credits = new();

    private ApplyWeeklyPlan CreateUseCase() => new(_households, _definitions);

    private EnsureOccurrencesGenerated CreateGenerator()
        => new(_households, _definitions, _occurrences, _assignments, _daysOff, _credits, new FixedTimeProvider(Now));

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
        => Assert.Null(await CreateUseCase().HandleAsync(Guid.NewGuid(), Wednesday, CancellationToken.None));

    /// <summary>
    /// Proves both halves of the "gäller framåt" requirement in one scenario: a task re-anchored
    /// to a new weekday (1) leaves its already-generated, still-outstanding occurrence exactly
    /// where it is, and (2) generates neither a duplicate NOR a gap once the new cadence takes
    /// over - see docs/ARCHITECTURE.md "Beslut: Placeringsalgoritmen".
    /// </summary>
    [Fact]
    public async Task Applying_a_plan_never_touches_outstanding_work_and_never_duplicates_or_skips_the_next_occurrence()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        var bathroom = household.AddArea("Badrum");
        var kitchen = household.AddArea("Kök");
        await _households.UpdateAsync(household, CancellationToken.None);

        // En större syssla (Kök) placeras girigt FÖRE den mindre (Badrum) och tar Måndag -
        // se WeeklyPlacementPlannerTests för samma mönster. Badrums-uppgiften hamnar då på
        // Tisdag i stället för sin gamla ankardag (fredag).
        var bigTask = TaskDefinition.Create(household.Id, "Städa köket", 40, Now);
        bigTask.AssignToArea(kitchen.Id);
        bigTask.ChangeEffort(TaskEffort.Medium);
        bigTask.SetDefaultResponsibleMember(anna.Id);
        bigTask.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        _definitions.Seed(bigTask);

        var underTest = TaskDefinition.Create(household.Id, "Skrubba handfatet", 20, Now);
        underTest.AssignToArea(bathroom.Id);
        underTest.ChangeEffort(TaskEffort.Medium);
        underTest.SetDefaultResponsibleMember(anna.Id);
        underTest.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(underTest);

        // Redan utlagt, ännu ej klart - en förfallen förekomst från den GAMLA ankardagen.
        var outstanding = underTest.ScheduleFor(OldFriday, Now);
        _occurrences.Seed(outstanding);

        // bigTask var redan på Måndag (planen väljer samma dag igen) - bara underTest, som
        // flyttas från fredag till tisdag, räknas som ändrad.
        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, CancellationToken.None);
        Assert.Equal(1, changedCount);

        var reloadedBig = await _definitions.FindByIdAsync(household.Id, bigTask.Id, CancellationToken.None);
        var reloadedUnderTest = await _definitions.FindByIdAsync(household.Id, underTest.Id, CancellationToken.None);
        Assert.Equal(DayOfWeek.Monday, reloadedBig!.Recurrence!.Weekday);
        Assert.Equal(DayOfWeek.Tuesday, reloadedUnderTest!.Recurrence!.Weekday);

        // 1) Redan utlagt arbete rörs aldrig - exakt samma förekomst, exakt samma datum.
        var stillOutstanding = (await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None))
            .Single(o => o.TaskDefinitionId == underTest.Id);
        Assert.Equal(OldFriday, stillOutstanding.ScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, stillOutstanding.Status);

        // Nästa tisdag efter 2026-03-04 är 2026-03-10.
        var newTuesday = new DateOnly(2026, 3, 10);

        // 2) Framåt: exakt EN ny förekomst på den nya dagen - varken en dubblett eller ett hopp.
        await CreateGenerator().HandleAsync(household.Id, newTuesday, CancellationToken.None);

        var afterFirstRun = await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None);
        var underTestOccurrences = afterFirstRun.Where(o => o.TaskDefinitionId == underTest.Id).ToList();
        Assert.Equal(2, underTestOccurrences.Count); // den gamla + exakt en ny
        Assert.Contains(underTestOccurrences, o => o.OriginalScheduledDate == OldFriday);
        Assert.Contains(underTestOccurrences, o => o.OriginalScheduledDate == newTuesday);

        // Ett andra anrop samma dag ska inte duplicera.
        var addCountBefore = _occurrences.AddCallCount;
        await CreateGenerator().HandleAsync(household.Id, newTuesday, CancellationToken.None);
        Assert.Equal(addCountBefore, _occurrences.AddCallCount);

        // En vecka senare: precis EN till, ingen har hoppats över.
        await CreateGenerator().HandleAsync(household.Id, newTuesday.AddDays(7), CancellationToken.None);
        var afterSecondWeek = (await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None))
            .Where(o => o.TaskDefinitionId == underTest.Id)
            .ToList();
        Assert.Equal(3, afterSecondWeek.Count);
        Assert.Contains(afterSecondWeek, o => o.OriginalScheduledDate == newTuesday.AddDays(7));
    }

    [Fact]
    public async Task A_plain_monthly_task_becomes_weekday_anchored()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(60));
        await _households.UpdateAsync(household, CancellationToken.None);

        var monthly = TaskDefinition.Create(household.Id, "Betala räkningar", 10, Now);
        monthly.SetDefaultResponsibleMember(anna.Id);
        monthly.SetRecurrence(RecurrenceRule.Monthly(OldFriday));
        _definitions.Seed(monthly);

        await CreateUseCase().HandleAsync(household.Id, Wednesday, CancellationToken.None);

        var reloaded = await _definitions.FindByIdAsync(household.Id, monthly.Id, CancellationToken.None);
        Assert.NotNull(reloaded!.Recurrence!.MonthlyWeek);
        Assert.NotNull(reloaded.Recurrence.Weekday);
    }

    [Fact]
    public async Task Two_monthly_tasks_on_the_same_weekday_spread_across_different_weeks_of_the_month()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        // Bara måndagar tillåter Heavy - tvingar BÅDA Heavy-besöken till samma veckodag, så
        // spridningen över veckor i månaden faktiskt sätts på prov. WeeklyEffortCeiling.Create
        // defaultar OLISTADE dagar till Heavy ("ingen begränsning"), så varje dag måste sättas
        // explicit här för att verkligen snäva in till bara måndag.
        anna.ChangeWeeklyEffortCeiling(WeeklyEffortCeiling.Create(
            Enum.GetValues<DayOfWeek>().ToDictionary(
                day => day, day => day == DayOfWeek.Monday ? TaskEffort.Heavy : TaskEffort.Light)));
        await _households.UpdateAsync(household, CancellationToken.None);

        // Två fristående "Övrigt"-uppgifter (ingen area) - var för sig egna besök.
        var first = TaskDefinition.Create(household.Id, "Rensa garage", 30, Now);
        first.ChangeEffort(TaskEffort.Heavy);
        first.SetDefaultResponsibleMember(anna.Id);
        first.SetRecurrence(RecurrenceRule.MonthlyOnWeekday(OldFriday, WeekOfMonth.First, DayOfWeek.Wednesday));
        _definitions.Seed(first);

        var second = TaskDefinition.Create(household.Id, "Rensa förråd", 30, Now);
        second.ChangeEffort(TaskEffort.Heavy);
        second.SetDefaultResponsibleMember(anna.Id);
        second.SetRecurrence(RecurrenceRule.MonthlyOnWeekday(OldFriday, WeekOfMonth.First, DayOfWeek.Wednesday));
        _definitions.Seed(second);

        await CreateUseCase().HandleAsync(household.Id, Wednesday, CancellationToken.None);

        var reloadedFirst = await _definitions.FindByIdAsync(household.Id, first.Id, CancellationToken.None);
        var reloadedSecond = await _definitions.FindByIdAsync(household.Id, second.Id, CancellationToken.None);

        Assert.Equal(DayOfWeek.Monday, reloadedFirst!.Recurrence!.Weekday);
        Assert.Equal(DayOfWeek.Monday, reloadedSecond!.Recurrence!.Weekday);
        Assert.NotEqual(reloadedFirst.Recurrence.MonthlyWeek, reloadedSecond.Recurrence.MonthlyWeek);
    }
}
