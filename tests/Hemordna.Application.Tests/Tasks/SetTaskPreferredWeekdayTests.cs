using Hemordna.Application.Households;
using Hemordna.Application.Tasks;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

/// <summary>
/// "Alltid på en viss veckodag" (Björns krav) - locking an existing task to a weekday. See
/// docs/ARCHITECTURE.md "Beslut: Alltid på en viss veckodag".
/// </summary>
public class SetTaskPreferredWeekdayTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-04 is a Wednesday; 2026-02-27 is the Friday of the previous week.
    private static readonly DateOnly Wednesday = new(2026, 3, 4);
    private static readonly DateOnly OldFriday = new(2026, 2, 27);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryTaskDefinitionRepository _definitions = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly InMemoryTaskAssignmentRepository _assignments = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly InMemoryMemberTimeCreditRepository _credits = new();

    private SetTaskPreferredWeekday CreateUseCase() => new(_definitions);

    private EnsureOccurrencesGenerated CreateGenerator()
        => new(_households, _definitions, _occurrences, _assignments, _daysOff, _credits, new FixedTimeProvider(Now));

    private async Task<Guid> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return household.Id;
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_task()
        => Assert.Null(await CreateUseCase().HandleAsync(
            Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Tuesday, Wednesday, CancellationToken.None));

    [Fact]
    public async Task Locking_a_weekly_task_re_anchors_its_recurrence_to_the_new_day()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Släng soporna", 10, Now);
        task.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            householdId, task.Id, DayOfWeek.Tuesday, Wednesday, CancellationToken.None);

        Assert.Equal(DayOfWeek.Tuesday, result!.PreferredWeekday);
        Assert.Equal(DayOfWeek.Tuesday, result.Recurrence!.Weekday);
    }

    /// <summary>Same "no duplicate, no skipped period" guarantee ApplyWeeklyPlan already relies
    /// on (RecurrenceReanchoring) - an already-generated, still-outstanding occurrence from the
    /// OLD day is left exactly where it is, and exactly one new occurrence appears on the new
    /// day going forward, no duplicate and no gap.</summary>
    [Fact]
    public async Task Locking_a_task_to_a_new_day_neither_duplicates_nor_skips_the_next_occurrence()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Släng soporna", 10, Now);
        task.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(task);

        // Redan utlagt, ännu ej klart - en förfallen förekomst från den GAMLA dagen.
        var outstanding = task.ScheduleFor(OldFriday, Now);
        _occurrences.Seed(outstanding);

        await CreateUseCase().HandleAsync(householdId, task.Id, DayOfWeek.Tuesday, Wednesday, CancellationToken.None);

        // 1) Redan utlagt arbete rörs aldrig.
        var stillOutstanding = (await _occurrences.ListOutstandingByHouseholdAsync(householdId, CancellationToken.None))
            .Single(o => o.TaskDefinitionId == task.Id);
        Assert.Equal(OldFriday, stillOutstanding.ScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, stillOutstanding.Status);

        // Nästa tisdag efter 2026-03-04 är 2026-03-10.
        var newTuesday = new DateOnly(2026, 3, 10);

        // 2) Framåt: exakt EN ny förekomst på den nya dagen - varken en dubblett eller ett hopp.
        await CreateGenerator().HandleAsync(householdId, newTuesday, CancellationToken.None);

        var afterFirstRun = await _occurrences.ListOutstandingByHouseholdAsync(householdId, CancellationToken.None);
        Assert.Equal(2, afterFirstRun.Count); // den gamla + exakt en ny
        Assert.Contains(afterFirstRun, o => o.OriginalScheduledDate == OldFriday);
        Assert.Contains(afterFirstRun, o => o.OriginalScheduledDate == newTuesday);
    }

    [Fact]
    public async Task Rejects_locking_a_daily_task()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Diska", 5, Now);
        task.SetRecurrence(RecurrenceRule.Daily(OldFriday));
        _definitions.Seed(task);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            householdId, task.Id, DayOfWeek.Tuesday, Wednesday, CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_locking_an_as_needed_task()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Handla mat", 30, Now);
        task.SetStaleAfterDays(7);
        _definitions.Seed(task);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            householdId, task.Id, DayOfWeek.Tuesday, Wednesday, CancellationToken.None));
    }

    [Fact]
    public async Task Clearing_the_lock_does_not_touch_the_current_recurrence()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Släng soporna", 10, Now);
        task.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        task.SetPreferredWeekday(DayOfWeek.Friday);
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(householdId, task.Id, null, Wednesday, CancellationToken.None);

        Assert.Null(result!.PreferredWeekday);
        Assert.Equal(DayOfWeek.Friday, result.Recurrence!.Weekday);
    }

    [Fact]
    public async Task Locking_to_the_day_it_already_sits_on_changes_nothing_meaningful()
    {
        var householdId = await ArrangeHouseholdAsync();
        var task = TaskDefinition.Create(householdId, "Släng soporna", 10, Now);
        task.SetRecurrence(RecurrenceRule.Weekly(OldFriday, DayOfWeek.Friday));
        _definitions.Seed(task);

        var result = await CreateUseCase().HandleAsync(
            householdId, task.Id, DayOfWeek.Friday, Wednesday, CancellationToken.None);

        Assert.Equal(DayOfWeek.Friday, result!.PreferredWeekday);
        Assert.Equal(DayOfWeek.Friday, result.Recurrence!.Weekday);
    }
}
