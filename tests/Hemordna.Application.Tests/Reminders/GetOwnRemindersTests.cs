using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class GetOwnRemindersTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private GetOwnReminders CreateUseCase() => new(_reminders);

    private Reminder Seed(
        Guid? memberId = null,
        DateOnly? date = null,
        string title = "Tandläkare")
    {
        var reminder = Reminder.Create(
            HouseholdId, memberId ?? AnnaId, title, null, date ?? Friday, null, CreatedAt);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Returns_the_members_own_reminder_in_range()
    {
        Seed();

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task Excludes_a_reminder_outside_the_date_range()
    {
        Seed(date: Friday.AddDays(10));

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Excludes_a_cancelled_reminder()
    {
        var reminder = Seed();
        reminder.Cancel();

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Does_not_return_another_members_reminder_in_the_same_household()
    {
        Seed(memberId: BjornId);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Empty(result);
    }

    /// <summary>
    /// ListForMemberInRangeAsync (Min dag/Vecka's "Dina påminnelser") never filters on
    /// <see cref="ReminderAudience"/> or <see cref="Reminder.Shares"/> - those only ever govern
    /// what OTHER members see (<see cref="GetHouseholdReminders"/>). An owner always sees their
    /// own reminder, whatever they chose for everyone else.
    /// </summary>
    [Fact]
    public async Task The_owners_own_view_is_unaffected_by_audience_and_shares()
    {
        var reminder = Seed();
        reminder.ChangeVisibility(ReminderVisibility.Household);
        reminder.SetAudience(ReminderAudience.Selected, []);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Friday, Friday, CancellationToken.None);

        Assert.Single(result);
    }
}
