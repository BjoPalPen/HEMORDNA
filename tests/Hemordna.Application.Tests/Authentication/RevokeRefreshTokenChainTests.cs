using Hemordna.Application.Authentication;
using Hemordna.Application.Tests.Households;

namespace Hemordna.Application.Tests.Authentication;

public class RevokeRefreshTokenChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();

    private RevokeRefreshTokenChain CreateUseCase() => new(_refreshTokens, new FixedTimeProvider(Now));

    private Task<IssuedRefreshToken> IssueAsync(Guid userId)
        => new IssueRefreshToken(_refreshTokens, new FixedTimeProvider(Now))
            .HandleAsync(userId, Lifetime, CancellationToken.None);

    [Fact]
    public async Task Revokes_the_presented_tokens_chain()
    {
        var userId = Guid.NewGuid();
        var issued = await IssueAsync(userId);

        await CreateUseCase().HandleAsync(issued.Token, CancellationToken.None);

        var token = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(issued.Token), CancellationToken.None);
        Assert.NotNull(token);
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public async Task Never_touches_another_users_chain()
    {
        var owner = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var issued = await IssueAsync(owner);
        var otherIssued = await IssueAsync(otherUser);

        await CreateUseCase().HandleAsync(issued.Token, CancellationToken.None);

        var otherToken = await _refreshTokens.FindByHashAsync(
            RefreshTokenSecret.Hash(otherIssued.Token), CancellationToken.None);
        Assert.NotNull(otherToken);
        Assert.True(otherToken.IsActive(Now));
    }

    [Fact]
    public async Task An_unknown_token_is_a_silent_no_op()
    {
        // Must not throw, and the response is identical whether or not the value ever existed -
        // see the remarks on RevokeRefreshTokenChain.HandleAsync.
        await CreateUseCase().HandleAsync("not-a-real-token", CancellationToken.None);

        Assert.Empty(_refreshTokens.All);
    }
}
