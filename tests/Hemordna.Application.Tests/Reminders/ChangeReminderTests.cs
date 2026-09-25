using Hemordna.Application.Reminders;
using Hemordna.Domain.Common;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class ChangeReminderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private ChangeReminderTitle ChangeTitle() => new(_reminders);

    private ChangeReminderLocation ChangeLocation() => new(_reminders);

    private ChangeReminderVisibility ChangeVisibility() => new(_reminders);

    private Reminder Seed(Guid? memberId = null, string? location = "Folktandvården")
    {
        var reminder = Reminder.Create(
            HouseholdId, memberId ?? AnnaId, "Tandläkare", location, Friday, null, CreatedAt);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Changes_the_title_of_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await ChangeTitle()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, "Ny tandläkartid", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ny tandläkartid", result.Title);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found_for_title()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await ChangeTitle()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, "Kapad titel", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        // Unchanged - proves the attempt never touched the other member's reminder.
        Assert.Equal("Tandläkare", reminder.Title);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_title()
    {
        var result = await ChangeTitle()
            .HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), "Ny titel", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_cancelled_reminder_rejects_a_title_change_uncaught()
    {
        var reminder = Seed();
        reminder.Cancel();

        await Assert.ThrowsAsync<DomainException>(() => ChangeTitle()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, "Ny titel", CancellationToken.None));
    }

    [Fact]
    public async Task Changes_the_location_of_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await ChangeLocation()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, "Ny klinik", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ny klinik", result.Location);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found_for_location()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await ChangeLocation()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, "Kapad plats", CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal("Folktandvården", reminder.Location);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_location()
    {
        var result = await ChangeLocation()
            .HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), "Ny plats", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Changes_the_visibility_of_the_callers_own_reminder()
    {
        var reminder = Seed();

        var result = await ChangeVisibility()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, ReminderVisibility.Household, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderVisibility.Household, result.Visibility);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found_for_visibility()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await ChangeVisibility()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, ReminderVisibility.Household, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        // Unchanged - proves the attempt never touched the other member's reminder.
        Assert.Equal(ReminderVisibility.Private, reminder.Visibility);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing_for_visibility()
    {
        var result = await ChangeVisibility()
            .HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), ReminderVisibility.Household, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_cancelled_reminder_rejects_a_visibility_change_uncaught()
    {
        var reminder = Seed();
        reminder.Cancel();

        await Assert.ThrowsAsync<DomainException>(() => ChangeVisibility()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, ReminderVisibility.Household, CancellationToken.None));
    }
}
