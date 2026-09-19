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
        => Assert.Null(await CreateUseCase().HandleAsync(Guid.NewGuid(), Wednesday, null, CancellationToken.None));

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
        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);
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

        await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);

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

        await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);

        var reloadedFirst = await _definitions.FindByIdAsync(household.Id, first.Id, CancellationToken.None);
        var reloadedSecond = await _definitions.FindByIdAsync(household.Id, second.Id, CancellationToken.None);

        Assert.Equal(DayOfWeek.Monday, reloadedFirst!.Recurrence!.Weekday);
        Assert.Equal(DayOfWeek.Monday, reloadedSecond!.Recurrence!.Weekday);
        Assert.NotEqual(reloadedFirst.Recurrence.MonthlyWeek, reloadedSecond.Recurrence.MonthlyWeek);
    }

    /// <summary>
    /// "Alltid på en viss veckodag" (Björns krav) - ApplyWeeklyPlan must never move a locked
    /// task, even under a scenario that WOULD move it if it were treated as an ordinary
    /// unlocked visit. Both tasks are the same VisitKind/effort, so without the lock they would
    /// compete on size in the normal greedy order (biggest first) and swap outcomes: the big
    /// task would claim Monday and the small one would be pushed to Tuesday instead - see
    /// docs/ARCHITECTURE.md "Beslut: Alltid på en viss veckodag".
    /// </summary>
    [Fact]
    public async Task Applying_a_plan_never_moves_a_task_locked_to_a_weekday()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        await _households.UpdateAsync(household, CancellationToken.None);

        // Locked to Monday - soptömning på hämtningsdagen, ett krav, inte ett önskemål. Small
        // (10 min), so an ordinary greedy pass would place it AFTER the big task below.
        var locked = TaskDefinition.Create(household.Id, "Släng soporna", 10, Now);
        locked.SetDefaultResponsibleMember(anna.Id);
        locked.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        locked.SetPreferredWeekday(DayOfWeek.Monday);
        _definitions.Seed(locked);

        // Unlocked, big (100 min) - an ordinary greedy pass would place THIS one first and claim
        // Monday (all days tied at 120 initially), pushing the small task to Tuesday instead.
        var big = TaskDefinition.Create(household.Id, "Storstäda källaren", 100, Now);
        big.SetDefaultResponsibleMember(anna.Id);
        big.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(big);

        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, null, CancellationToken.None);

        // The big task moved (Friday -> Tuesday, since Monday's 110 remaining minutes, after the
        // locked visit's 10 were counted in first, is now less than every other day's 120); the
        // locked one did not, even though - unlocked - it would have been outcompeted for Monday.
        Assert.Equal(1, changedCount);
        var reloadedLocked = await _definitions.FindByIdAsync(household.Id, locked.Id, CancellationToken.None);
        var reloadedBig = await _definitions.FindByIdAsync(household.Id, big.Id, CancellationToken.None);
        Assert.Equal(DayOfWeek.Monday, reloadedLocked!.Recurrence!.Weekday);
        Assert.Equal(DayOfWeek.Monday, reloadedLocked.PreferredWeekday);
        Assert.Equal(DayOfWeek.Tuesday, reloadedBig!.Recurrence!.Weekday);
    }

    /// <summary>
    /// Björns beslut: "ett flyttat besök låses" - proves all three parts of it in one scenario,
    /// the same way <see cref="Applying_a_plan_never_touches_outstanding_work_and_never_duplicates_or_skips_the_next_occurrence"/>
    /// does for the ordinary greedy path: (1) every task in the moved visit gets
    /// TaskDefinition.PreferredWeekday set to the chosen day, (2) already-generated outstanding
    /// work is untouched, and (3) the next occurrence is neither a duplicate nor skipped - via
    /// the exact same RecurrenceReanchoring technique already proven for a manual lock.
    /// </summary>
    [Fact]
    public async Task A_moved_visit_locks_every_task_in_it_without_touching_outstanding_work_or_duplicating_the_next_occurrence()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        var bathroom = household.AddArea("Badrum");
        await _households.UpdateAsync(household, CancellationToken.None);

        var underTest = TaskDefinition.Create(household.Id, "Skrubba handfatet", 20, Now);
        underTest.AssignToArea(bathroom.Id);
        underTest.ChangeEffort(TaskEffort.Medium);
        underTest.SetDefaultResponsibleMember(anna.Id);
        underTest.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(underTest);

        // Redan utlagt, ännu ej klart - en förfallen förekomst från den GAMLA dagen.
        var outstanding = underTest.ScheduleFor(OldFriday, Now);
        _occurrences.Seed(outstanding);

        var moves = new Dictionary<Guid, DayOfWeek> { [underTest.Id] = DayOfWeek.Tuesday };
        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, moves, CancellationToken.None);
        Assert.Equal(1, changedCount);

        var reloaded = await _definitions.FindByIdAsync(household.Id, underTest.Id, CancellationToken.None);
        Assert.Equal(DayOfWeek.Tuesday, reloaded!.Recurrence!.Weekday);
        Assert.Equal(DayOfWeek.Tuesday, reloaded.PreferredWeekday); // Beslut: ett flyttat besök låses

        // 1) Redan utlagt arbete rörs aldrig.
        var stillOutstanding = (await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None))
            .Single(o => o.TaskDefinitionId == underTest.Id);
        Assert.Equal(OldFriday, stillOutstanding.ScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, stillOutstanding.Status);

        // 2) Framåt: exakt EN ny förekomst på den nya dagen - varken en dubblett eller ett hopp.
        // Nästa tisdag efter 2026-03-04 är 2026-03-10.
        var newTuesday = new DateOnly(2026, 3, 10);
        await CreateGenerator().HandleAsync(household.Id, newTuesday, CancellationToken.None);

        var afterRun = await _occurrences.ListOutstandingByHouseholdAsync(household.Id, CancellationToken.None);
        var underTestOccurrences = afterRun.Where(o => o.TaskDefinitionId == underTest.Id).ToList();
        Assert.Equal(2, underTestOccurrences.Count); // den gamla + exakt en ny
        Assert.Contains(underTestOccurrences, o => o.OriginalScheduledDate == OldFriday);
        Assert.Contains(underTestOccurrences, o => o.OriginalScheduledDate == newTuesday);
    }

    /// <summary>Flytt av ett besök som redan var låst till en ANNAN dag byter låset till den nya
    /// dagen i stället för att skippas som "redan låst" (den ordinarie, olåsta grenens regel).</summary>
    [Fact]
    public async Task Moving_an_already_locked_visit_changes_the_lock_to_the_new_day()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        await _households.UpdateAsync(household, CancellationToken.None);

        var locked = TaskDefinition.Create(household.Id, "Släng soporna", 10, Now);
        locked.SetDefaultResponsibleMember(anna.Id);
        locked.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        locked.SetPreferredWeekday(DayOfWeek.Monday);
        _definitions.Seed(locked);

        var moves = new Dictionary<Guid, DayOfWeek> { [locked.Id] = DayOfWeek.Thursday };
        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, moves, CancellationToken.None);

        Assert.Equal(1, changedCount);
        var reloaded = await _definitions.FindByIdAsync(household.Id, locked.Id, CancellationToken.None);
        Assert.Equal(DayOfWeek.Thursday, reloaded!.PreferredWeekday);
        Assert.Equal(DayOfWeek.Thursday, reloaded.Recurrence!.Weekday);
    }

    /// <summary>Idempotens: att "flytta" ett besök till dagen det redan är låst till (t.ex. ett
    /// andra Använd-klick utan mellanliggande ändring) räknas inte som en förändring.</summary>
    [Fact]
    public async Task Moving_a_visit_to_the_day_it_is_already_locked_to_does_not_count_as_changed()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        await _households.UpdateAsync(household, CancellationToken.None);

        var locked = TaskDefinition.Create(household.Id, "Släng soporna", 10, Now);
        locked.SetDefaultResponsibleMember(anna.Id);
        locked.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Monday));
        locked.SetPreferredWeekday(DayOfWeek.Monday);
        _definitions.Seed(locked);

        var moves = new Dictionary<Guid, DayOfWeek> { [locked.Id] = DayOfWeek.Monday };
        var changedCount = await CreateUseCase().HandleAsync(household.Id, Wednesday, moves, CancellationToken.None);

        Assert.Equal(0, changedCount);
    }

    /// <summary>
    /// A sparse rule (Interval > 1) is never placed, so "Använd" must never re-anchor it either -
    /// its own StartDate carries WHICH month/phase is meant, and re-anchoring to "today" would
    /// destroy that. See docs/ARCHITECTURE.md "Beslut: Glesa regler lämnas i fred". The weekly
    /// task's own placement is the control: if the sparse task were (wrongly) treated as
    /// placeable it would also compete for a weekday and get reanchored, making changedCount 2
    /// instead of 1.
    /// </summary>
    [Fact]
    public async Task Applying_a_plan_leaves_a_sparse_monthly_task_untouched_and_uncounted()
    {
        var sparseNow = new DateTimeOffset(2026, 9, 19, 8, 0, 0, TimeSpan.Zero);
        var today = new DateOnly(2026, 9, 19);
        var oldAnchor = new DateOnly(2026, 8, 28);

        var household = await new CreateHousehold(_households, new FixedTimeProvider(sparseNow))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();
        anna.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(120));
        await _households.UpdateAsync(household, CancellationToken.None);

        var weekly = TaskDefinition.Create(household.Id, "Dammsug golvet", 20, sparseNow);
        weekly.SetDefaultResponsibleMember(anna.Id);
        weekly.SetRecurrence(RecurrenceRule.Weekly(oldAnchor, DayOfWeek.Friday));
        _definitions.Seed(weekly);

        var sparseAnchor = new DateOnly(2027, 5, 1);
        var sparseRule = RecurrenceRule.Monthly(sparseAnchor, everyNMonths: 12);
        var sparse = TaskDefinition.Create(household.Id, "Rensa altanmöbler", 60, sparseNow);
        sparse.SetDefaultResponsibleMember(anna.Id);
        sparse.SetRecurrence(sparseRule);
        _definitions.Seed(sparse);

        var changedCount = await CreateUseCase().HandleAsync(household.Id, today, null, CancellationToken.None);
        Assert.Equal(1, changedCount);

        var reloadedSparse = await _definitions.FindByIdAsync(household.Id, sparse.Id, CancellationToken.None);
        Assert.Equal(sparseRule, reloadedSparse!.Recurrence);
        Assert.Null(reloadedSparse.PreferredWeekday);
    }
}
