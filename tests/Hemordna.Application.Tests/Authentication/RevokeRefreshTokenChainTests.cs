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
    public async Task Revokes_the_callers_own_chain()
    {
        var userId = Guid.NewGuid();
        var issued = await IssueAsync(userId);

        await CreateUseCase().HandleAsync(userId, issued.Token, CancellationToken.None);

        var token = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(issued.Token), CancellationToken.None);
        Assert.NotNull(token);
        Assert.False(token.IsActive(Now));
    }

    [Fact]
    public async Task Presenting_another_users_token_is_a_silent_no_op()
    {
        var owner = Guid.NewGuid();
        var impostor = Guid.NewGuid();
        var issued = await IssueAsync(owner);

        // Must not throw, and must not reveal anything by its outcome - see the remarks on
        // RevokeRefreshTokenChain.HandleAsync.
        await CreateUseCase().HandleAsync(impostor, issued.Token, CancellationToken.None);

        var token = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(issued.Token), CancellationToken.None);
        Assert.NotNull(token);
        Assert.True(token.IsActive(Now));
    }

    [Fact]
    public async Task An_unknown_token_is_a_silent_no_op()
    {
        await CreateUseCase().HandleAsync(Guid.NewGuid(), "not-a-real-token", CancellationToken.None);

        Assert.Empty(_refreshTokens.All);
    }
}
