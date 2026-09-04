using Hemordna.Application.Households;

namespace Hemordna.Application.Tests.Households;

public class GetHouseholdTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 6, 9, 15, 0, TimeSpan.Zero);

    private readonly InMemoryHouseholdRepository _households = new();

    [Fact]
    public async Task Returns_the_household_when_it_exists()
    {
        var created = await new CreateHousehold(_households, new FixedTimeProvider(Now))
            .HandleAsync("Familjen", CancellationToken.None);

        var found = await new GetHousehold(_households)
            .HandleAsync(created.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Familjen", found.Name);
    }

    [Fact]
    public async Task Returns_null_when_the_household_does_not_exist()
    {
        var found = await new GetHousehold(_households)
            .HandleAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(found);
    }
}
