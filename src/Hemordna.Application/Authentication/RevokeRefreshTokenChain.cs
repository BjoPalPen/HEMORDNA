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
    /// Revokes the chain <paramref name="rawToken"/> belongs to. Takes no separate caller
    /// identity to check it against - by design, not an oversight: possessing the raw value
    /// already lets its holder rotate it into a fresh access token for that user (see
    /// <see cref="RotateRefreshToken"/>), so requiring proof of identity beyond the token itself
    /// here would protect nothing that is not already exposed by the more powerful operation.
    /// This also means logging out never depends on the caller's access token still being valid
    /// - an anonymous call with just the refresh token is enough, which matters for the exact
    /// moment logout is most needed: after being away long enough that the access token expired.
    /// An unknown token is a silent no-op - nothing in the response reveals whether it ever
    /// existed.
    /// </summary>
    public async Task HandleAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenSecret.Hash(rawToken);
        var existing = await _refreshTokens.FindByHashAsync(hash, cancellationToken);

        if (existing is null)
        {
            return;
        }

        await _refreshTokens.RevokeChainAsync(existing.ChainId, _timeProvider.GetUtcNow(), cancellationToken);
    }
}
