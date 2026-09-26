using Hemordna.Application.Authentication;
using Hemordna.Application.Tests.Households;

namespace Hemordna.Application.Tests.Authentication;

public class IssueRefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();

    private IssueRefreshToken CreateUseCase() => new(_refreshTokens, new FixedTimeProvider(Now));

    [Fact]
    public async Task Issues_a_token_that_expires_after_the_given_lifetime()
    {
        var userId = Guid.NewGuid();

        var issued = await CreateUseCase().HandleAsync(userId, Lifetime, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(issued.Token));
        Assert.Equal(Now + Lifetime, issued.ExpiresAt);
    }

    [Fact]
    public async Task Persists_a_token_that_can_be_found_by_hashing_the_issued_value_again()
    {
        var userId = Guid.NewGuid();

        var issued = await CreateUseCase().HandleAsync(userId, Lifetime, CancellationToken.None);

        var stored = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(issued.Token), CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(userId, stored.UserId);
        Assert.True(stored.IsActive(Now));
    }

    [Fact]
    public async Task Never_persists_the_raw_token_value()
    {
        var issued = await CreateUseCase().HandleAsync(Guid.NewGuid(), Lifetime, CancellationToken.None);

        var stored = Assert.Single(_refreshTokens.All);
        Assert.NotEqual(issued.Token, stored.TokenHash);
    }

    [Fact]
    public async Task Two_issuances_for_the_same_user_start_different_chains()
    {
        var userId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await useCase.HandleAsync(userId, Lifetime, CancellationToken.None);
        await useCase.HandleAsync(userId, Lifetime, CancellationToken.None);

        var chainIds = _refreshTokens.All.Select(token => token.ChainId).Distinct();
        Assert.Equal(2, chainIds.Count());
    }
}
