using Hemordna.Application.Reminders;
using Hemordna.Domain.Common;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class MoveReminderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();
    private static readonly Guid AnnaId = Guid.NewGuid();
    private static readonly Guid BjornId = Guid.NewGuid();

    private readonly InMemoryReminderRepository _reminders = new();

    private MoveReminder CreateUseCase() => new(_reminders);

    private Reminder Seed(Guid? memberId = null)
    {
        var reminder = Reminder.Create(
            HouseholdId, memberId ?? AnnaId, "Tandläkare", null, Friday, new TimeOnly(9, 0), CreatedAt);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Moves_the_callers_own_reminder_to_a_new_date_and_time()
    {
        var reminder = Seed();
        var monday = Friday.AddDays(3);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, monday, new TimeOnly(13, 15), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(monday, result.Date);
        Assert.Equal(new TimeOnly(13, 15), result.TimeOfDay);
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found()
    {
        var reminder = Seed(memberId: BjornId);

        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, Friday.AddDays(1), null, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(Friday, reminder.Date);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing()
    {
        var result = await CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, Guid.NewGuid(), Friday.AddDays(1), null, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_cancelled_reminder_rejects_a_move_uncaught()
    {
        var reminder = Seed();
        reminder.Cancel();

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .HandleAsync(HouseholdId, AnnaId, reminder.Id, Friday.AddDays(1), null, CancellationToken.None));
    }
}
