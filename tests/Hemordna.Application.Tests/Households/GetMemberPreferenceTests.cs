using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class GetMemberPreferenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberPreferenceRepository _preferences = new();

    private GetMemberPreference CreateUseCase() => new(_households, _preferences);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        return (household.Id, household.Members.Single());
    }

    [Fact]
    public async Task Returns_defaults_for_a_member_who_never_set_a_preference()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var preference = await CreateUseCase().HandleAsync(householdId, member.Id, CancellationToken.None);

        Assert.NotNull(preference);
        Assert.Equal(PresentationMode.Text, preference.Presentation);
        Assert.Equal(MotivationLevel.None, preference.Motivation);
        Assert.Equal(0, _preferences.Count);
    }

    [Fact]
    public async Task Returns_the_saved_preference_once_one_exists()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        await new SetMemberPreference(_households, _preferences).HandleAsync(
            householdId, member.Id, PresentationMode.OneAtATime, MotivationLevel.Calm, CancellationToken.None);

        var preference = await CreateUseCase().HandleAsync(householdId, member.Id, CancellationToken.None);

        Assert.NotNull(preference);
        Assert.Equal(PresentationMode.OneAtATime, preference.Presentation);
        Assert.Equal(MotivationLevel.Calm, preference.Motivation);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var preference = await CreateUseCase().HandleAsync(householdId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(preference);
    }
}
