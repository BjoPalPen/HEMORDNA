using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class SetMemberPreferenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberPreferenceRepository _preferences = new();

    private SetMemberPreference CreateUseCase() => new(_households, _preferences);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Sets_the_preference_for_a_member_without_one_yet()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var preference = await CreateUseCase().HandleAsync(
            householdId, member.Id, PresentationMode.LargeText, MotivationLevel.Calm, CancellationToken.None);

        Assert.NotNull(preference);
        Assert.Equal(PresentationMode.LargeText, preference.Presentation);
        Assert.Equal(MotivationLevel.Calm, preference.Motivation);
        Assert.Equal(1, _preferences.Count);
    }

    [Fact]
    public async Task Setting_it_twice_updates_rather_than_duplicates()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        var useCase = CreateUseCase();

        await useCase.HandleAsync(
            householdId, member.Id, PresentationMode.LargeText, MotivationLevel.None, CancellationToken.None);
        var second = await useCase.HandleAsync(
            householdId, member.Id, PresentationMode.OneAtATime, MotivationLevel.Calm, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(PresentationMode.OneAtATime, second.Presentation);
        Assert.Equal(1, _preferences.Count);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var preference = await CreateUseCase().HandleAsync(
            householdId, Guid.NewGuid(), PresentationMode.Text, MotivationLevel.None, CancellationToken.None);

        Assert.Null(preference);
    }
}
