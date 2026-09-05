using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Application.Tests.Households;

public class JoinHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 9, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid JoiningUserId = Guid.NewGuid();

    private readonly InMemoryHouseholdRepository _households = new();

    private JoinHousehold CreateUseCase() => new(_households, new FixedTimeProvider(Now));

    private async Task<Household> SeedHouseholdAsync()
    {
        var household = Household.Create("Familjen", CreatedAt);
        await _households.AddAsync(household, CancellationToken.None);
        return household;
    }

    [Fact]
    public async Task Adds_the_joining_user_as_a_new_member()
    {
        var seeded = await SeedHouseholdAsync();

        var household = await CreateUseCase().HandleAsync(
            seeded.InviteCode, JoiningUserId, "Björn", CancellationToken.None);

        Assert.NotNull(household);
        var member = Assert.Single(household.Members);
        Assert.Equal("Björn", member.DisplayName);
        Assert.Equal(JoiningUserId, member.UserId);
    }

    [Fact]
    public async Task The_new_member_starts_with_no_time_allocated()
    {
        var seeded = await SeedHouseholdAsync();

        var household = await CreateUseCase().HandleAsync(
            seeded.InviteCode, JoiningUserId, "Björn", CancellationToken.None);

        var member = Assert.Single(household!.Members);
        Assert.Equal(0, member.WeeklyTimeBudget.TotalWeeklyMinutes);
    }

    [Fact]
    public async Task Matching_is_case_insensitive_and_ignores_surrounding_whitespace()
    {
        var seeded = await SeedHouseholdAsync();

        var household = await CreateUseCase().HandleAsync(
            $"  {seeded.InviteCode.ToLowerInvariant()}  ", JoiningUserId, "Björn", CancellationToken.None);

        Assert.NotNull(household);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_code_without_persisting_anything()
    {
        var household = await CreateUseCase().HandleAsync(
            "NOSUCH01", JoiningUserId, "Björn", CancellationToken.None);

        Assert.Null(household);
        Assert.Equal(0, _households.UpdateCallCount);
    }

    [Fact]
    public async Task Rejects_a_display_name_already_used_in_that_household()
    {
        var seeded = await SeedHouseholdAsync();
        seeded.AddMember("Björn", WeeklyTimeBudget.Empty, CreatedAt);
        await _households.UpdateAsync(seeded, CancellationToken.None);

        await Assert.ThrowsAsync<Domain.Common.DomainException>(() => CreateUseCase().HandleAsync(
            seeded.InviteCode, JoiningUserId, "björn", CancellationToken.None));
    }
}
