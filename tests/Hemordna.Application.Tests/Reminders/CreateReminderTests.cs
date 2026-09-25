using Hemordna.Application.Households;
using Hemordna.Application.Reminders;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Common;
using Hemordna.Domain.Households;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class CreateReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryReminderRepository _reminders = new();

    private CreateReminder CreateUseCase() => new(_households, _reminders, new FixedTimeProvider(Now));

    private async Task<(Guid HouseholdId, Guid MemberId)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single().Id);
    }

    private async Task<(Guid HouseholdId, Guid OwnerId, Guid OtherMemberId)> ArrangeHouseholdWithTwoMembersAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var bjorn = household.AddMember("Björn", WeeklyTimeBudget.Empty, Now);
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household.Id, household.Members.Single(member => member.DisplayName == "Anna").Id, bjorn.Id);
    }

    [Fact]
    public async Task Creates_an_upcoming_reminder_for_the_caller()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, memberId, "Tandläkare", "Folktandvården", Friday, new TimeOnly(9, 0), null,
            null, null, null, CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(householdId, reminder.HouseholdId);
        Assert.Equal(memberId, reminder.MemberId);
        Assert.Equal("Tandläkare", reminder.Title);
        Assert.Equal("Folktandvården", reminder.Location);
        Assert.Equal(Friday, reminder.Date);
        Assert.Equal(new TimeOnly(9, 0), reminder.TimeOfDay);
        Assert.Null(reminder.TravelMinutes);
        Assert.Equal(ReminderStatus.Upcoming, reminder.Status);
        Assert.Equal(ReminderVisibility.Private, reminder.Visibility);
        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
        Assert.Equal(1, _reminders.AddCallCount);
    }

    [Fact]
    public async Task Creates_a_reminder_with_travel_minutes_alongside_a_time_of_day()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, memberId, "Tandläkare", "Folktandvården", Friday, new TimeOnly(14, 0), 30,
            null, null, null, CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(30, reminder.TravelMinutes);
    }

    [Fact]
    public async Task Travel_minutes_without_a_time_of_day_is_rejected_by_the_domain_uncaught()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .HandleAsync(householdId, memberId, "Tandläkare", null, Friday, null, 30, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase()
            .HandleAsync(householdId, Guid.NewGuid(), "Tandläkare", null, Friday, null, null, null, null, null, CancellationToken.None);

        Assert.Null(reminder);
        Assert.Equal(0, _reminders.AddCallCount);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var reminder = await CreateUseCase()
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), "Tandläkare", null, Friday, null, null, null, null, null, CancellationToken.None);

        Assert.Null(reminder);
    }

    [Fact]
    public async Task An_invalid_title_is_rejected_by_the_domain_uncaught()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => CreateUseCase()
            .HandleAsync(householdId, memberId, "   ", null, Friday, null, null, null, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task An_explicit_visibility_is_used_on_the_created_reminder()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, memberId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, null, null, CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(ReminderVisibility.Household, reminder.Visibility);
    }

    [Fact]
    public async Task An_omitted_visibility_defaults_to_private()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, memberId, "Tandläkare", null, Friday, null, null, null, null, null, CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(ReminderVisibility.Private, reminder.Visibility);
    }

    [Fact]
    public async Task Creates_a_reminder_pre_shared_with_a_selected_audience_in_one_call()
    {
        var (householdId, ownerId, otherMemberId) = await ArrangeHouseholdWithTwoMembersAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, ownerId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, ReminderAudience.Selected, [otherMemberId], CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(ReminderAudience.Selected, reminder.Audience);
        Assert.Equal([otherMemberId], reminder.Shares.Select(share => share.MemberId));
        Assert.Equal(1, _reminders.AddCallCount);
    }

    [Fact]
    public async Task An_omitted_audience_defaults_to_everyone_with_no_shares()
    {
        var (householdId, memberId) = await ArrangeHouseholdAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, memberId, "Tandläkare", null, Friday, null, null, null, null, null, CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    /// <summary>
    /// The backdoor check: creating a reminder must not be a way around the same membership
    /// validation <see cref="SetReminderAudience"/> enforces - an unknown or outside id must
    /// never be saved, whether it arrives at creation time or in a later change. Nothing is
    /// created at all, not even with the audience silently dropped.
    /// </summary>
    [Fact]
    public async Task An_audience_member_id_outside_the_household_is_rejected_and_nothing_is_created()
    {
        var (householdId, ownerId, _) = await ArrangeHouseholdWithTwoMembersAsync();
        var outsiderId = Guid.NewGuid();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, ownerId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, ReminderAudience.Selected, [outsiderId], CancellationToken.None);

        Assert.Null(reminder);
        Assert.Equal(0, _reminders.AddCallCount);
    }

    [Fact]
    public async Task An_inactive_audience_member_id_is_rejected_and_nothing_is_created()
    {
        var (householdId, ownerId, otherMemberId) = await ArrangeHouseholdWithTwoMembersAsync();
        var household = await _households.FindByIdAsync(householdId, CancellationToken.None);
        household!.DeactivateMember(otherMemberId);
        await _households.UpdateAsync(household, CancellationToken.None);

        var reminder = await CreateUseCase().HandleAsync(
            householdId, ownerId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, ReminderAudience.Selected, [otherMemberId], CancellationToken.None);

        Assert.Null(reminder);
        Assert.Equal(0, _reminders.AddCallCount);
    }

    [Fact]
    public async Task The_everyone_audience_ignores_any_member_ids_given_alongside_it()
    {
        var (householdId, ownerId, _) = await ArrangeHouseholdWithTwoMembersAsync();

        var reminder = await CreateUseCase().HandleAsync(
            householdId, ownerId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, ReminderAudience.Everyone, [Guid.NewGuid()], CancellationToken.None);

        Assert.NotNull(reminder);
        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public async Task The_owner_naming_themselves_in_the_audience_is_rejected_by_the_domain_uncaught()
    {
        var (householdId, ownerId, _) = await ArrangeHouseholdWithTwoMembersAsync();

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            householdId, ownerId, "Tandläkare", null, Friday, null, null,
            ReminderVisibility.Household, ReminderAudience.Selected, [ownerId], CancellationToken.None));
    }
}
