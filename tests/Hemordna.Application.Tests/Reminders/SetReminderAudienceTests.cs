using Hemordna.Application.Households;
using Hemordna.Application.Reminders;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Common;
using Hemordna.Domain.Households;
using Hemordna.Domain.Reminders;

namespace Hemordna.Application.Tests.Reminders;

public class SetReminderAudienceTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryReminderRepository _reminders = new();

    private SetReminderAudience CreateUseCase() => new(_households, _reminders);

    private async Task<(Household Household, Guid OwnerId, Guid OtherMemberId)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var bjorn = household.AddMember("Björn", WeeklyTimeBudget.Empty, Now);
        await _households.UpdateAsync(household, CancellationToken.None);

        return (household, household.Members.Single(member => member.DisplayName == "Anna").Id, bjorn.Id);
    }

    private Reminder Seed(Guid householdId, Guid memberId)
    {
        var reminder = Reminder.Create(
            householdId, memberId, "Tandläkare", null, Friday, null, Now,
            visibility: ReminderVisibility.Household);
        _reminders.Seed(reminder);
        return reminder;
    }

    [Fact]
    public async Task Sets_selected_audience_with_an_active_household_member()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [otherMemberId], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderAudience.Selected, result.Audience);
        Assert.Equal([otherMemberId], result.Shares.Select(share => share.MemberId));
        Assert.Equal(1, _reminders.UpdateCallCount);
    }

    [Fact]
    public async Task Sets_everyone_audience_and_clears_any_previous_shares()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);
        reminder.SetAudience(ReminderAudience.Selected, [otherMemberId]);

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Everyone, [], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderAudience.Everyone, result.Audience);
        Assert.Empty(result.Shares);
    }

    /// <summary>
    /// Fail-closed, proven rather than assumed: an empty selected list is a valid, deliberate
    /// state where nobody sees the time - see docs/ARCHITECTURE.md, "Beslut: Synlighet för
    /// påminnelser".
    /// </summary>
    [Fact]
    public async Task Selected_with_an_empty_list_is_accepted_and_leaves_nobody_sharing()
    {
        var (household, ownerId, _) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [], CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ReminderAudience.Selected, result.Audience);
        Assert.Empty(result.Shares);
    }

    /// <summary>The single most important rule in this whole feature: a reminder is private to
    /// its owner, even within the same household. See PRODUCT.md §11 and CLAUDE.md §9.</summary>
    [Fact]
    public async Task Another_members_reminder_is_treated_as_not_found()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, otherMemberId);

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [otherMemberId], CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        // Unchanged - proves the attempt never touched the other member's reminder.
        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
    }

    [Fact]
    public async Task An_unknown_reminder_finds_nothing()
    {
        var (household, ownerId, _) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, Guid.NewGuid(), ReminderAudience.Everyone, [], CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task An_id_that_is_not_a_member_of_the_household_is_rejected()
    {
        var (household, ownerId, _) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);
        var outsiderId = Guid.NewGuid();

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [outsiderId], CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Equal(ReminderAudience.Everyone, reminder.Audience);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public async Task An_inactive_members_id_is_rejected()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);
        household.DeactivateMember(otherMemberId);
        await _households.UpdateAsync(household, CancellationToken.None);

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [otherMemberId], CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, _reminders.UpdateCallCount);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public async Task A_valid_id_alongside_an_unknown_id_is_rejected_entirely()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);
        var outsiderId = Guid.NewGuid();

        var result = await CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected,
            [otherMemberId, outsiderId], CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(reminder.Shares);
    }

    [Fact]
    public async Task A_cancelled_reminder_rejects_an_audience_change_uncaught()
    {
        var (household, ownerId, otherMemberId) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);
        reminder.Cancel();

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [otherMemberId], CancellationToken.None));
    }

    [Fact]
    public async Task The_owner_naming_themselves_is_rejected_by_the_domain_uncaught()
    {
        var (household, ownerId, _) = await ArrangeHouseholdAsync();
        var reminder = Seed(household.Id, ownerId);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().HandleAsync(
            household.Id, ownerId, reminder.Id, ReminderAudience.Selected, [ownerId], CancellationToken.None));
    }
}
