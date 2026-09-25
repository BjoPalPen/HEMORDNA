namespace Hemordna.Application.Authentication;

/// <summary>
/// Revokes every refresh token a user has, across every chain - called on a successful password
/// change. Without this, a stolen password combined with a still-valid refresh token would
/// survive the very fix meant to lock the thief out (uppdragets problem statement: a long JWT
/// cannot be revoked, which is why rotation exists in the first place - a password change that
/// leaves refresh tokens standing would reintroduce the same hole one level down).
/// </summary>
public sealed class RevokeAllRefreshTokensForUser
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public RevokeAllRefreshTokensForUser(IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
    {
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
    }

    public Task HandleAsync(Guid userId, CancellationToken cancellationToken)
        => _refreshTokens.RevokeAllForUserAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);
}
