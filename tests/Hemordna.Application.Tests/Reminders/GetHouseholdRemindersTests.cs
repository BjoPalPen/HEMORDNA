using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class GetHouseholdRemindersTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private GetHouseholdReminders CreateUseCase() => new(_reminders);

    private Reminder Seed(
        Guid? memberId = null,
        Guid? householdId = null,
        DateOnly? date = null,
        TimeOnly? timeOfDay = null,
        string title = "Tandläkare",
        ReminderVisibility visibility = ReminderVisibility.Household,
        ReminderAudience? audience = null,
        IReadOnlyCollection<Guid>? sharedWith = null)
    {
        var reminder = Reminder.Create(
            householdId ?? HouseholdId,
            memberId ?? BjornId,
            title,
            "Folktandvården",
            date ?? Friday,
            timeOfDay,
            CreatedAt,
            visibility: visibility);

        if (audience is not null)
        {
            reminder.SetAudience(audience.Value, sharedWith ?? []);
        }

        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task A_private_reminder_never_comes_back()
    {
        Seed(visibility: ReminderVisibility.Private);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_busy_only_reminder_has_no_title()
    {
        Seed(visibility: ReminderVisibility.BusyOnly, title: "Tandläkare");

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        var view = Assert.Single(result);
        Assert.Null(view.Title);
    }

    [Fact]
    public async Task A_household_reminder_carries_its_title()
    {
        Seed(visibility: ReminderVisibility.Household, title: "Tandläkare");

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        var view = Assert.Single(result);
        Assert.Equal("Tandläkare", view.Title);
    }

    [Theory]
    [InlineData(ReminderVisibility.BusyOnly)]
    [InlineData(ReminderVisibility.Household)]
    public async Task The_callers_own_reminder_is_never_returned_regardless_of_visibility(
        ReminderVisibility visibility)
    {
        Seed(memberId: AnnaId, visibility: visibility);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_cancelled_reminder_is_excluded()
    {
        var reminder = Seed(visibility: ReminderVisibility.Household);
        reminder.Cancel();

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_checked_off_reminder_is_still_included()
    {
        var reminder = Seed(visibility: ReminderVisibility.Household);
        reminder.CheckOff();

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Another_households_reminder_is_never_returned()
    {
        Seed(householdId: Guid.NewGuid(), visibility: ReminderVisibility.Household);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_reminder_outside_the_date_range_is_excluded()
    {
        Seed(visibility: ReminderVisibility.Household, date: Friday.AddDays(10));

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task The_view_carries_the_owners_member_id_and_time_of_day()
    {
        Seed(memberId: BjornId, visibility: ReminderVisibility.Household, timeOfDay: new TimeOnly(14, 0));

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        var view = Assert.Single(result);
        Assert.Equal(BjornId, view.MemberId);
        Assert.Equal(new TimeOnly(14, 0), view.TimeOfDay);
    }

    [Fact]
    public async Task A_selected_member_sees_a_reminder_shared_with_them()
    {
        Seed(audience: ReminderAudience.Selected, sharedWith: [AnnaId]);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task A_member_not_in_the_selected_list_does_not_see_the_reminder()
    {
        var ceciliaId = Guid.NewGuid();
        Seed(audience: ReminderAudience.Selected, sharedWith: [ceciliaId]);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task The_everyone_audience_is_visible_to_every_member_unchanged()
    {
        Seed(audience: ReminderAudience.Everyone);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Single(result);
    }

    /// <summary>
    /// The fail-closed guarantee itself: an empty selected list must never be read as "everyone",
    /// however the condition ends up being written - see docs/ARCHITECTURE.md, "Beslut:
    /// Synlighet för påminnelser".
    /// </summary>
    [Fact]
    public async Task Selected_with_an_empty_list_is_visible_to_nobody()
    {
        Seed(audience: ReminderAudience.Selected, sharedWith: []);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task A_share_in_a_different_household_is_never_returned()
    {
        Seed(householdId: Guid.NewGuid(), audience: ReminderAudience.Selected, sharedWith: [AnnaId]);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }
}
