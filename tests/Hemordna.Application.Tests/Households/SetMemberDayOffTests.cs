using Hemordna.Application.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Application.Tests.Tasks;
using Hemordna.Domain.Common;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Households;

public class SetMemberDayOffTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);

    // 2026-03-02 is a Monday.
    private static readonly DateOnly Today = new(2026, 3, 2);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly InMemoryTaskOccurrenceRepository _occurrences = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private SetMemberDayOff CreateUseCase() => new(_households, _daysOff, _occurrences, _notifier);

    private async Task<(Guid HouseholdId, HouseholdMember Anna)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();

        return (household.Id, anna);
    }

    private TaskOccurrence SeedOccurrence(
        Guid householdId, Guid memberId, DateOnly date, bool canBeDeferred = true)
    {
        var definition = TaskDefinition.Create(householdId, $"Uppgift {Guid.NewGuid()}", 10, Now);
        definition.SetCanBeDeferred(canBeDeferred);

        var occurrence = definition.ScheduleFor(date, Now);
        occurrence.AssignTo(memberId);
        _occurrences.Seed(occurrence);

        return occurrence;
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_member()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(
            householdId, Guid.NewGuid(), Tomorrow, Today, DayOffMode.BringAllForward, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task BringAllForward_moves_every_own_occurrence_on_that_date_to_today()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        var first = SeedOccurrence(householdId, anna.Id, Tomorrow);
        var second = SeedOccurrence(householdId, anna.Id, Tomorrow);

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.BringAllForward, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.BroughtForward);
        Assert.Equal(0, result.Deferred);
        Assert.Null(result.DeferredTo);

        Assert.Equal(Today, first.ScheduledDate);
        Assert.Equal(Tomorrow, first.OriginalScheduledDate);
        Assert.Equal(Today, second.ScheduledDate);
        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task DeferAll_moves_every_own_occurrence_on_that_date_to_the_next_available_day()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        var occurrence = SeedOccurrence(householdId, anna.Id, Tomorrow);

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.DeferAll, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.Deferred);
        Assert.Equal(Tomorrow.AddDays(1), result.DeferredTo);
        Assert.Equal(Tomorrow.AddDays(1), occurrence.ScheduledDate);
        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task DeferAll_skips_a_day_that_is_also_a_day_off()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        var occurrence = SeedOccurrence(householdId, anna.Id, Tomorrow);

        // The day right after the one being cleared is ALSO a day off - DeferAll must not land
        // work there either.
        _daysOff.Seed(MemberDayOff.Create(householdId, anna.Id, Tomorrow.AddDays(1), Today));

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.DeferAll, CancellationToken.None);

        Assert.Equal(Tomorrow.AddDays(2), result!.DeferredTo);
        Assert.Equal(Tomorrow.AddDays(2), occurrence.ScheduledDate);
    }

    [Fact]
    public async Task DeferAll_throws_when_no_free_day_exists_within_14_days()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        SeedOccurrence(householdId, anna.Id, Tomorrow);

        // Fourteen consecutive days off - each individually valid to CREATE only because it is
        // backdated to whatever "today" would have had to be for that particular date to sit
        // exactly at the domain's own 7-day limit (MemberDayOff.Create rejects anything further
        // out). This mirrors the only way such a run could actually build up in production: one
        // day-off request at a time, added on consecutive real days, never all at once.
        for (var offset = 1; offset <= 14; offset++)
        {
            var date = Tomorrow.AddDays(offset);
            _daysOff.Seed(MemberDayOff.Create(householdId, anna.Id, date, today: date.AddDays(-7)));
        }

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.DeferAll, CancellationToken.None));
    }

    [Fact]
    public async Task DeferAll_leaves_a_non_deferrable_occurrence_exactly_where_it_is()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        var occurrence = SeedOccurrence(householdId, anna.Id, Tomorrow, canBeDeferred: false);

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.DeferAll, CancellationToken.None);

        Assert.Equal(0, result!.Deferred);
        Assert.Null(result.DeferredTo);
        Assert.Equal(Tomorrow, occurrence.ScheduledDate);
        Assert.Equal(0, _notifier.CallCount);
    }

    [Fact]
    public async Task Setting_the_same_date_twice_does_not_duplicate_the_day_off()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();

        await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.BringAllForward, CancellationToken.None);
        await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.BringAllForward, CancellationToken.None);

        Assert.Equal(1, _daysOff.Count);
    }

    [Fact]
    public async Task BringAllForward_on_today_itself_is_a_safe_no_op_rather_than_throwing()
    {
        // Marking TODAY itself off means everything in "mine" is already scheduled for today -
        // TaskOccurrence.BringForwardTo treats moving something to the date it is already on as
        // an invariant violation (it throws), so the use case must recognise this ahead of time
        // rather than let that surface as a failed request for someone who just wanted the day
        // off with nothing already there to move.
        var (householdId, anna) = await ArrangeHouseholdAsync();
        var occurrence = SeedOccurrence(householdId, anna.Id, Today);

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Today, Today, DayOffMode.BringAllForward, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result.BroughtForward);
        Assert.Equal(Today, occurrence.ScheduledDate);
        Assert.Equal(0, _notifier.CallCount);
    }

    [Fact]
    public async Task Nothing_planned_on_that_date_still_succeeds_with_zero_counts()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(
            householdId, anna.Id, Tomorrow, Today, DayOffMode.BringAllForward, CancellationToken.None);

        Assert.Equal(0, result!.BroughtForward);
        Assert.Equal(0, result.Deferred);
        Assert.Equal(0, _notifier.CallCount);
        Assert.Equal(1, _daysOff.Count);
    }
}
