using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class SetMemberAvailabilityTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);

    // 2026-02-06 is a Friday.
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberAvailabilityRepository _availabilities = new();

    private SetMemberAvailability CreateUseCase() => new(_households, _availabilities);

    private async Task<(Guid HouseholdId, HouseholdMember Member)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);

        var member = household.Members.Single();
        member.ChangeWeeklyTimeBudget(WeeklyTimeBudget.Uniform(30));

        return (household.Id, member);
    }

    [Fact]
    public async Task Records_less_time_for_a_single_day()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var availability = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, 5, CancellationToken.None);

        Assert.NotNull(availability);
        Assert.Equal(5, availability.AvailableMinutes);
        Assert.Equal(Friday, availability.Date);

        // The normal week is untouched by a single day's change.
        Assert.Equal(30, member.WeeklyTimeBudget.MinutesFor(DayOfWeek.Friday));
        Assert.Equal(30, member.AvailableMinutesOn(Friday.AddDays(1), availabilityOverride: null));
    }

    [Fact]
    public async Task Zero_minutes_is_a_valid_answer()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();

        var availability = await CreateUseCase()
            .HandleAsync(householdId, member.Id, Friday, 0, CancellationToken.None);

        Assert.NotNull(availability);
        Assert.Equal(0, availability.AvailableMinutes);
    }

    [Fact]
    public async Task Setting_it_twice_updates_rather_than_duplicates()
    {
        var (householdId, member) = await ArrangeHouseholdAsync();
        var useCase = CreateUseCase();

        await useCase.HandleAsync(householdId, member.Id, Friday, 15, CancellationToken.None);
        var second = await useCase.HandleAsync(householdId, member.Id, Friday, 5, CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(5, second.AvailableMinutes);
        Assert.Equal(1, _availabilities.Count);
    }

    [Fact]
    public async Task Returns_null_for_a_member_outside_the_household()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var availability = await CreateUseCase()
            .HandleAsync(householdId, Guid.NewGuid(), Friday, 5, CancellationToken.None);

        Assert.Null(availability);
        Assert.Equal(0, _availabilities.Count);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_household()
    {
        var availability = await CreateUseCase()
            .HandleAsync(Guid.NewGuid(), Guid.NewGuid(), Friday, 5, CancellationToken.None);

        Assert.Null(availability);
    }
}
