using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class PauseHouseholdMemberTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Until = new(2026, 3, 10);

    private readonly InMemoryHouseholdRepository _households = new();

    private PauseHouseholdMember CreateUseCase() => new(_households);

    [Fact]
    public async Task Pauses_the_named_member_through_the_given_date()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(household.Id, member.Id, Until, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(Until, updated.PausedUntil);
        Assert.Equal(1, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Does_not_pause_other_members()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var anna = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        var bjorn = household.AddMember("Bjorn", WeeklyTimeBudget.Empty, CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);

        await CreateUseCase().HandleAsync(household.Id, anna.Id, Until, CancellationToken.None);

        Assert.Null(bjorn.PausedUntil);
    }

    [Fact]
    public async Task Null_resumes_an_already_paused_member()
    {
        var household = Household.Create("Familjen", CreatedAt);
        var member = household.AddMember("Anna", WeeklyTimeBudget.Empty, CreatedAt);
        member.Pause(Until);
        await _households.AddAsync(household, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(household.Id, member.Id, null, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Null(updated.PausedUntil);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_member()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);

        var updated = await CreateUseCase().HandleAsync(household.Id, Guid.NewGuid(), Until, CancellationToken.None);

        Assert.Null(updated);
        Assert.Equal(0, _households.UpdateCallCount);
    }
}
