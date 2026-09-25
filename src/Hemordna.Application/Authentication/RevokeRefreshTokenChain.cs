namespace Hemordna.Application.Authentication;

/// <summary>Revokes the whole rotation chain a presented refresh token belongs to - the
/// operation behind <c>POST /api/auth/logout</c>.</summary>
public sealed class RevokeRefreshTokenChain
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public RevokeRefreshTokenChain(IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
    {
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Revokes the chain <paramref name="rawToken"/> belongs to, provided it actually belongs to
    /// <paramref name="callerUserId"/>. An unknown token and someone else's token are handled
    /// identically - silently, doing nothing - per uppdragets säkerhetskrav: "en refresh-token
    /// hör till en användare[;] att presentera någon annans ska behandlas exakt som en ogiltig
    /// - samma svar, ingen ledtråd om att den finns." A caller who is already logged out, or who
    /// sends a garbled value, gets the same successful-looking no-op as one who tries to log out
    /// with a token that was never theirs.
    /// </summary>
    public async Task HandleAsync(Guid callerUserId, string rawToken, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenSecret.Hash(rawToken);
        var existing = await _refreshTokens.FindByHashAsync(hash, cancellationToken);

        if (existing is null || existing.UserId != callerUserId)
        {
            return;
        }

        await _refreshTokens.RevokeChainAsync(existing.ChainId, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
