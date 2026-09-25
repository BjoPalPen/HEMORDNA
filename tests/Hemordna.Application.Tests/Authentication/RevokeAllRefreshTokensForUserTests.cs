using Hemordna.Application.Authentication;
using Hemordna.Application.Tests.Households;

namespace Hemordna.Application.Tests.Authentication;

public class RevokeAllRefreshTokensForUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();

    private RevokeAllRefreshTokensForUser CreateUseCase() => new(_refreshTokens, new FixedTimeProvider(Now));

    [Fact]
    public async Task Revokes_every_chain_belonging_to_the_user()
    {
        var userId = Guid.NewGuid();
        var issue = new IssueRefreshToken(_refreshTokens, new FixedTimeProvider(Now));
        await issue.HandleAsync(userId, Lifetime, CancellationToken.None);
        await issue.HandleAsync(userId, Lifetime, CancellationToken.None);

        await CreateUseCase().HandleAsync(userId, CancellationToken.None);

        Assert.All(_refreshTokens.All, token => Assert.False(token.IsActive(Now)));
    }

    [Fact]
    public async Task Does_not_touch_another_users_tokens()
    {
        var targetUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var issue = new IssueRefreshToken(_refreshTokens, new FixedTimeProvider(Now));
        await issue.HandleAsync(targetUser, Lifetime, CancellationToken.None);
        var otherIssued = await issue.HandleAsync(otherUser, Lifetime, CancellationToken.None);

        await CreateUseCase().HandleAsync(targetUser, CancellationToken.None);

        var otherToken = await _refreshTokens.FindByHashAsync(
            RefreshTokenSecret.Hash(otherIssued.Token), CancellationToken.None);
        Assert.NotNull(otherToken);
        Assert.True(otherToken.IsActive(Now));
    }
}
