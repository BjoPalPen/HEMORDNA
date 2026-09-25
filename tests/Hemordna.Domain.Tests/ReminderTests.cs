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
        Guid? memberId = null,
        int? travelMinutes = null,
        ReminderVisibility? visibility = null)
        => Reminder.Create(
            householdId ?? Guid.NewGuid(),
            memberId ?? Guid.NewGuid(),
            title,
            location,
            date ?? Friday,
            timeOfDay,
            CreatedAt,
            travelMinutes,
            visibility ?? ReminderVisibility.Private);

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

    [Fact]
    public void Restore_sets_a_cancelled_reminder_back_to_upcoming()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        reminder.Restore();

        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
    }

    [Fact]
    public void Restore_on_a_reminder_that_is_not_cancelled_throws()
    {
        var reminder = CreateReminder();

        Assert.Throws<DomainException>(() => reminder.Restore());
    }

    [Fact]
    public void A_restored_reminder_can_have_its_title_changed_again()
    {
        var reminder = CreateReminder();
        reminder.Cancel();
        reminder.Restore();

        reminder.ChangeTitle("Nytt namn");

        Assert.Equal("Nytt namn", reminder.Title);
    }

    [Fact]
    public void A_restored_reminder_can_have_its_location_changed_again()
    {
        var reminder = CreateReminder();
        reminder.Cancel();
        reminder.Restore();

        reminder.ChangeLocation("Ny plats");

        Assert.Equal("Ny plats", reminder.Location);
    }

    [Fact]
    public void A_restored_reminder_can_be_moved_again()
    {
        var reminder = CreateReminder();
        reminder.Cancel();
        reminder.Restore();

        reminder.MoveTo(Friday.AddDays(1), new TimeOnly(10, 0));

        Assert.Equal(Friday.AddDays(1), reminder.Date);
        Assert.Equal(new TimeOnly(10, 0), reminder.TimeOfDay);
    }

    [Fact]
    public void Travel_minutes_can_be_set_on_creation_alongside_a_time_of_day()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0), travelMinutes: 30);

        Assert.Equal(30, reminder.TravelMinutes);
    }

    [Fact]
    public void Setting_travel_minutes_on_creation_without_a_time_of_day_throws()
    {
        Assert.Throws<DomainException>(() => CreateReminder(timeOfDay: null, travelMinutes: 30));
    }

    [Fact]
    public void SetTravelMinutes_without_a_time_of_day_throws()
    {
        var reminder = CreateReminder(timeOfDay: null);

        Assert.Throws<DomainException>(() => reminder.SetTravelMinutes(30));
    }

    [Fact]
    public void SetTravelMinutes_with_a_time_of_day_is_saved()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));

        reminder.SetTravelMinutes(45);

        Assert.Equal(45, reminder.TravelMinutes);
    }

    [Fact]
    public void SetTravelMinutes_can_clear_the_travel_time()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0), travelMinutes: 30);

        reminder.SetTravelMinutes(null);

        Assert.Null(reminder.TravelMinutes);
    }

    [Fact]
    public void Zero_travel_minutes_is_rejected()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => reminder.SetTravelMinutes(0));
    }

    [Fact]
    public void Negative_travel_minutes_is_rejected()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => reminder.SetTravelMinutes(-5));
    }

    [Fact]
    public void Travel_minutes_beyond_the_maximum_is_rejected()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => reminder.SetTravelMinutes(Reminder.MaxTravelMinutes + 1));
    }

    [Fact]
    public void Travel_minutes_at_the_maximum_is_accepted()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));

        reminder.SetTravelMinutes(Reminder.MaxTravelMinutes);

        Assert.Equal(Reminder.MaxTravelMinutes, reminder.TravelMinutes);
    }

    [Fact]
    public void MoveTo_all_day_clears_travel_minutes()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0), travelMinutes: 30);

        reminder.MoveTo(Friday.AddDays(1), null);

        Assert.Null(reminder.TravelMinutes);
    }

    [Fact]
    public void MoveTo_keeping_a_time_of_day_does_not_clear_travel_minutes()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0), travelMinutes: 30);

        reminder.MoveTo(Friday.AddDays(1), new TimeOnly(9, 0));

        Assert.Equal(30, reminder.TravelMinutes);
    }

    [Fact]
    public void A_cancelled_reminder_cannot_have_its_travel_minutes_changed()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.SetTravelMinutes(30));
    }

    [Fact]
    public void Lapse_sets_the_status_to_checked_off()
    {
        var reminder = CreateReminder();

        reminder.CheckOff();

        Assert.Equal(ReminderStatus.CheckedOff, reminder.Status);
    }

    [Fact]
    public void Lapsing_twice_is_a_no_op()
    {
        var reminder = CreateReminder();

        reminder.CheckOff();
        reminder.CheckOff();

        Assert.Equal(ReminderStatus.CheckedOff, reminder.Status);
    }

    [Fact]
    public void A_cancelled_reminder_cannot_be_checked_off()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.CheckOff());
    }

    [Fact]
    public void A_checked_off_reminder_cannot_be_cancelled()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.Cancel());
    }

    [Fact]
    public void A_checked_off_reminder_cannot_have_its_title_changed()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.ChangeTitle("Nytt namn"));
    }

    [Fact]
    public void A_checked_off_reminder_cannot_have_its_location_changed()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.ChangeLocation("Ny plats"));
    }

    [Fact]
    public void A_checked_off_reminder_cannot_be_moved()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.MoveTo(Friday.AddDays(1), null));
    }

    [Fact]
    public void A_checked_off_reminder_cannot_have_its_travel_minutes_changed()
    {
        var reminder = CreateReminder(timeOfDay: new TimeOnly(14, 0));
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.SetTravelMinutes(30));
    }

    [Fact]
    public void Restore_sets_a_checked_off_reminder_back_to_upcoming()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        reminder.Restore();

        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
    }

    [Fact]
    public void A_restored_checked_off_reminder_can_be_checked_off_again()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();
        reminder.Restore();

        reminder.CheckOff();

        Assert.Equal(ReminderStatus.CheckedOff, reminder.Status);
    }

    [Fact]
    public void A_restored_checked_off_reminder_can_be_cancelled()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();
        reminder.Restore();

        reminder.Cancel();

        Assert.Equal(ReminderStatus.Cancelled, reminder.Status);
    }

    [Fact]
    public void A_new_reminder_is_private_by_default()
    {
        var reminder = Reminder.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tandläkare",
            "Folktandvården",
            Friday,
            null,
            CreatedAt);

        Assert.Equal(ReminderVisibility.Private, reminder.Visibility);
    }

    [Theory]
    [InlineData(ReminderVisibility.Private)]
    [InlineData(ReminderVisibility.BusyOnly)]
    [InlineData(ReminderVisibility.Household)]
    public void Create_with_an_explicit_visibility_respects_it(ReminderVisibility visibility)
    {
        var reminder = Reminder.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tandläkare",
            "Folktandvården",
            Friday,
            null,
            CreatedAt,
            travelMinutes: null,
            visibility: visibility);

        Assert.Equal(visibility, reminder.Visibility);
    }

    [Theory]
    [InlineData(ReminderVisibility.Private)]
    [InlineData(ReminderVisibility.BusyOnly)]
    [InlineData(ReminderVisibility.Household)]
    public void ChangeVisibility_sets_the_visibility(ReminderVisibility visibility)
    {
        var reminder = CreateReminder();

        reminder.ChangeVisibility(visibility);

        Assert.Equal(visibility, reminder.Visibility);
    }

    [Fact]
    public void A_cancelled_reminder_cannot_have_its_visibility_changed()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(() => reminder.ChangeVisibility(ReminderVisibility.Household));
    }

    [Fact]
    public void A_checked_off_reminder_cannot_have_its_visibility_changed()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(() => reminder.ChangeVisibility(ReminderVisibility.Household));
    }

    [Fact]
    public void A_new_reminder_has_the_everyone_audience_and_no_shares()
    {
        var reminder = CreateReminder();

        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public void SetAudience_to_selected_records_the_given_members()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();

        reminder.SetAudience(ReminderAudience.Selected, [memberA, memberB]);

        Assert.Equal(ReminderAudience.Selected, reminder.Audience);
        Assert.Equal(
            new[] { memberA, memberB }.Order(),
            reminder.Shares.Select(share => share.MemberId).Order());
    }

    [Fact]
    public void SetAudience_to_selected_with_an_empty_list_is_valid_and_nobody_sees_it()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);

        reminder.SetAudience(ReminderAudience.Selected, []);

        Assert.Equal(ReminderAudience.Selected, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public void SetAudience_replaces_the_share_list_atomically_rather_than_appending()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);
        var memberA = Guid.NewGuid();
        var memberB = Guid.NewGuid();
        reminder.SetAudience(ReminderAudience.Selected, [memberA]);

        reminder.SetAudience(ReminderAudience.Selected, [memberB]);

        Assert.Equal([memberB], reminder.Shares.Select(share => share.MemberId));
    }

    [Fact]
    public void SetAudience_to_everyone_clears_any_existing_shares()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);
        reminder.SetAudience(ReminderAudience.Selected, [Guid.NewGuid()]);

        reminder.SetAudience(ReminderAudience.Everyone, []);

        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public void SetAudience_to_everyone_ignores_and_clears_member_ids_passed_alongside_it()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);

        reminder.SetAudience(ReminderAudience.Everyone, [Guid.NewGuid(), Guid.NewGuid()]);

        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public void ChangeVisibility_to_private_clears_any_existing_shares()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);
        reminder.SetAudience(ReminderAudience.Selected, [Guid.NewGuid()]);

        reminder.ChangeVisibility(ReminderVisibility.Private);

        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public void The_owner_can_never_be_in_their_own_audience()
    {
        var ownerId = Guid.NewGuid();
        var reminder = CreateReminder(memberId: ownerId, visibility: ReminderVisibility.Household);

        Assert.Throws<DomainException>(
            () => reminder.SetAudience(ReminderAudience.Selected, [ownerId]));
    }

    [Fact]
    public void The_owner_cannot_be_smuggled_in_alongside_another_member()
    {
        var ownerId = Guid.NewGuid();
        var reminder = CreateReminder(memberId: ownerId, visibility: ReminderVisibility.Household);

        Assert.Throws<DomainException>(
            () => reminder.SetAudience(ReminderAudience.Selected, [Guid.NewGuid(), ownerId]));
    }

    [Fact]
    public void A_failed_SetAudience_call_does_not_change_the_existing_shares()
    {
        var ownerId = Guid.NewGuid();
        var reminder = CreateReminder(memberId: ownerId, visibility: ReminderVisibility.Household);
        var memberA = Guid.NewGuid();
        reminder.SetAudience(ReminderAudience.Selected, [memberA]);

        Assert.Throws<DomainException>(
            () => reminder.SetAudience(ReminderAudience.Selected, [ownerId]));

        Assert.Equal([memberA], reminder.Shares.Select(share => share.MemberId));
    }

    [Fact]
    public void An_empty_member_id_in_the_audience_is_rejected()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);

        Assert.Throws<ArgumentException>(
            () => reminder.SetAudience(ReminderAudience.Selected, [Guid.Empty]));
    }

    [Fact]
    public void SetAudience_deduplicates_repeated_member_ids()
    {
        var reminder = CreateReminder(visibility: ReminderVisibility.Household);
        var memberA = Guid.NewGuid();

        reminder.SetAudience(ReminderAudience.Selected, [memberA, memberA]);

        Assert.Equal([memberA], reminder.Shares.Select(share => share.MemberId));
    }

    [Fact]
    public void A_cancelled_reminder_cannot_have_its_audience_changed()
    {
        var reminder = CreateReminder();
        reminder.Cancel();

        Assert.Throws<DomainException>(
            () => reminder.SetAudience(ReminderAudience.Everyone, []));
    }

    [Fact]
    public void A_checked_off_reminder_cannot_have_its_audience_changed()
    {
        var reminder = CreateReminder();
        reminder.CheckOff();

        Assert.Throws<DomainException>(
            () => reminder.SetAudience(ReminderAudience.Everyone, []));
    }
}
