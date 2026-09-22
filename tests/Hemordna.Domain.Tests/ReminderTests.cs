using Hemordna.Domain.Common;
using Hemordna.Domain.Reminders;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class ReminderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private static Reminder CreateReminder(
        string title = "Tandläkare",
        string? location = "Folktandvården",
        DateOnly? date = null,
        TimeOnly? timeOfDay = null,
        Guid? householdId = null,
        Guid? memberId = null)
        => Reminder.Create(
            householdId ?? Guid.NewGuid(),
            memberId ?? Guid.NewGuid(),
            title,
            location,
            date ?? Friday,
            timeOfDay,
            CreatedAt);

    [Fact]
    public void A_new_reminder_is_upcoming()
    {
        var reminder = CreateReminder();

        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
    }

    [Fact]
    public void Household_and_member_ids_are_set_from_creation()
    {
        var householdId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var reminder = CreateReminder(householdId: householdId, memberId: memberId);

        Assert.Equal(householdId, reminder.HouseholdId);
        Assert.Equal(memberId, reminder.MemberId);
    }

    [Fact]
    public void An_empty_household_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateReminder(householdId: Guid.Empty));
    }

    [Fact]
    public void An_empty_member_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => CreateReminder(memberId: Guid.Empty));
    }

    [Fact]
    public void A_null_time_of_day_means_all_day()
    {
        var reminder = CreateReminder(timeOfDay: null);

        Assert.Null(reminder.TimeOfDay);
    }

    [Fact]
    public void A_time_of_day_can_be_set_on_creation()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 30));

        Assert.Equal(new TimeOnly(14, 30), reminder.TimeOfDay);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_or_whitespace_title_is_rejected(string title)
    {
        Assert.Throws<ArgumentException>(() => CreateReminder(title: title));
    }

    [Fact]
    public void A_title_is_trimmed()
    {
        var reminder = CreateReminder(title: "  Läkarbesök  ");

        Assert.Equal("Läkarbesök", reminder.Title);
    }

    [Fact]
    public void A_title_longer_than_the_maximum_is_rejected()
    {
        var tooLong = new string('a', Reminder.MaxTitleLength + 1);

        Assert.Throws<ArgumentException>(() => CreateReminder(title: tooLong));
    }

    [Fact]
    public void A_title_at_the_maximum_length_is_accepted()
    {
        var atLimit = new string('a', Reminder.MaxTitleLength);

        var reminder = CreateReminder(title: atLimit);

        Assert.Equal(atLimit, reminder.Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_or_whitespace_location_becomes_null(string? location)
    {
        var reminder = CreateReminder(location: location);

        Assert.Null(reminder.Location);
    }

    [Fact]
    public void A_location_is_trimmed()
    {
        var reminder = CreateReminder(location: "  Vårdcentralen  ");

        Assert.Equal("Vårdcentralen", reminder.Location);
    }

    [Fact]
    public void A_location_longer_than_the_maximum_is_rejected()
    {
        var tooLong = new string('a', Reminder.MaxLocationLength + 1);

        Assert.Throws<ArgumentException>(() => CreateReminder(location: tooLong));
    }

    [Fact]
    public void A_location_at_the_maximum_length_is_accepted()
    {
        var atLimit = new string('a', Reminder.MaxLocationLength);

        var reminder = CreateReminder(location: atLimit);

        Assert.Equal(atLimit, reminder.Location);
    }

    [Fact]
    public void A_date_far_beyond_the_supported_horizon_is_rejected()
    {
        var tooFarAhead = DateOnly.FromDateTime(CreatedAt.UtcDateTime).AddYears(SchedulingDate.MaxYearsAhead + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReminder(date: tooFarAhead));
    }

    [Fact]
    public void ChangeTitle_updates_the_title()
    {
        var reminder = CreateReminder(title: "Tandläkare");

        reminder.ChangeTitle("Ny tandläkartid");

        Assert.Equal("Ny tandläkartid", reminder.Title);
    }

    [Fact]
    public void ChangeLocation_updates_the_location()
    {
        var reminder = CreateReminder();

        reminder.ChangeLocation("Ny klinik");

        Assert.Equal("Ny klinik", reminder.Location);
    }

    [Fact]
    public void ChangeLocation_can_clear_the_location()
    {
        var reminder = CreateReminder();

        reminder.ChangeLocation("  ");

        Assert.Null(reminder.Location);
    }

    [Fact]
    public void MoveTo_updates_the_date_and_time()
    {
        var reminder = CreateReminder(date: Friday, timeOfDay: new TimeOnly(9, 0));
        var monday = Friday.AddDays(3);

        reminder.MoveTo(monday, new TimeOnly(13, 15));

        Assert.Equal(monday, reminder.Date);
        Assert.Equal(new TimeOnly(13, 15), reminder.TimeOfDay);
    }

    [Fact]
    public void MoveTo_can_clear_the_time_of_day()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(9, 0));

        reminder.MoveTo(Friday.AddDays(1), null);

        Assert.Null(reminder.TimeOfDay);
    }

    [Fact]
    public void MoveTo_rejects_a_date_outside_the_supported_calendar()
    {
        var reminder = CreateReminder();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => reminder.MoveTo(SchedulingDate.MaxSupportedDate.AddDays(1), null));
    }

    [Fact]
    public void Cancel_sets_the_status_to_cancelled()
    {
        var reminder = CreateReminder();

        reminder.Cancel();

        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    [Fact]
    public void Cancelling_twice_is_a_no_op()
    {
        var reminder = CreateReminder();

        reminder.Cancel();
        reminder.Cancel();

        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    [Fact]
    public void A_cancelled_reminder_cannot_have_its_title_changed()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.ChangeTitle("Nytt namn"));
    }

    [Fact]
    public void A_cancelled_reminder_cannot_have_its_location_changed()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.ChangeLocation("Ny plats"));
    }

    [Fact]
    public void A_cancelled_reminder_cannot_be_moved()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.MoveTo(Friday.AddDays(1), null));
    }
}
