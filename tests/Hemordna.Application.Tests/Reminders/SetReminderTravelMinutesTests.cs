using Hemordna.Application.Reminders;
using Hemordna.Domain.Common;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class SetReminderTravelMinutesTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private SetReminderTravelMinutes CreateUseCase() => new(_reminders);

    private static readonly TimeOnly DefaultTimeOfDay = new(14, 0);

    private Reminder Seed(Guid? memberId = null, bool allDay = false)
    {
        var reminder = Reminder.Create(
            HouseholdId, memberId ?? AnnaId, "Tandläkare", null, Friday,
            allDay ? null : DefaultTimeOfDay, CreatedAt);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Sets_the_travel_minutes_of_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, 30, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(30, result.TravelMinutes);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    [Fact]
    public async Task Clears_the_travel_minutes_with_null()
    {
        var reminder = Seed();
        reminder.SetTravelMinutes(30);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, null, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.TravelMinutes);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9. Proves it
    /// specifically for travel time - a fresh field could otherwise slip past the ownership
    /// check if a new use case forgot it.</summary>
    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, 30, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Null(reminder.TravelMinutes);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing()
    {
        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), 30, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_cancelled_reminder_rejects_a_travel_time_change_uncaught()
    {
        var reminder = Seed();
        reminder.Cancel();

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, 30, CancellationToken.None));
    }

    [Fact]
    public async Task Travel_minutes_without_a_time_of_day_is_rejected_uncaught()
    {
        var reminder = Seed(allDay: true);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, 30, CancellationToken.None));
    }
}
