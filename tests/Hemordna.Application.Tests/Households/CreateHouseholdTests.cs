using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class CreateHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 9, 15, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly InMemoryHouseholdRepository _households = new();

    private CreateHousehold CreateUseCase() => new(_households, new FixedTimeProvider(Now));

    private Task<Domain.Households.Household> CreateAsync(string name = "Familjen", string displayName = "Anna")
        => CreateUseCase().HandleAsync(name, UserId, displayName, CancellationToken.None);

    [Fact]
    public async Task Creates_and_persists_the_household()
    {
        var household = await CreateAsync();

        Assert.Equal("Familjen", household.Name);
        Assert.NotEqual(Guid.Empty, household.Id);
        Assert.Equal(1, _households.AddCallCount);

        var persisted = await _households.FindByIdAsync(household.Id, CancellationToken.None);
        Assert.Same(household, persisted);
    }

    [Fact]
    public async Task Adds_the_creating_user_as_the_first_member()
    {
        // A household with nobody in it cannot be planned for, so creation is not complete
        // until the creator is a member of it.
        var household = await CreateAsync(displayName: "Anna");

        var member = Assert.Single(household.Members);
        Assert.Equal("Anna", member.DisplayName);
        Assert.Equal(UserId, member.UserId);
        Assert.True(member.IsActive);
    }

    [Fact]
    public async Task The_first_member_starts_with_no_time_allocated()
    {
        var household = await CreateAsync();

        var member = Assert.Single(household.Members);
        Assert.Equal(0, member.WeeklyTimeBudget.TotalWeeklyMinutes);
    }

    [Fact]
    public async Task Stamps_the_household_with_the_injected_clock()
    {
        var household = await CreateAsync();

        Assert.Equal(Now, household.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_a_blank_name_without_persisting_anything(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateAsync(name));

        Assert.Equal(0, _households.AddCallCount);
    }

    [Fact]
    public async Task Two_households_may_share_a_name()
    {
        // Household names are not a system-wide unique key - plenty of homes are "Familjen".
        var first = await CreateAsync();
        var second = await CreateAsync();

        Assert.NotEqual(first.Id, second.Id);
    }
}
