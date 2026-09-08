using Hemordna.Application.Households;
using Hemordna.Application.Tests.Realtime;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class ClearMemberDayOffTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 3, 2);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    private readonly InMemoryHouseholdRepository _households = new();
    private readonly InMemoryMemberDayOffRepository _daysOff = new();
    private readonly SpyHouseholdNotifier _notifier = new();

    private ClearMemberDayOff CreateUseCase() => new(_households, _daysOff, _notifier);

    private async Task<(Guid HouseholdId, HouseholdMember Anna)> ArrangeHouseholdAsync()
    {
        var household = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", Guid.NewGuid(), "Anna", CancellationToken.None);
        var anna = household.Members.Single();

        return (household.Id, anna);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_member()
    {
        var (householdId, _) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(
            householdId, Guid.NewGuid(), Tomorrow, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Clears_an_existing_day_off_and_notifies()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();
        _daysOff.Seed(MemberDayOff.Create(householdId, anna.Id, Tomorrow, Today));

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Tomorrow, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, _daysOff.Count);
        Assert.Equal(1, _notifier.CallCount);
    }

    [Fact]
    public async Task Clearing_a_date_that_was_never_off_is_not_an_error()
    {
        var (householdId, anna) = await ArrangeHouseholdAsync();

        var result = await CreateUseCase().HandleAsync(householdId, anna.Id, Tomorrow, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, _daysOff.Count);
    }
}
