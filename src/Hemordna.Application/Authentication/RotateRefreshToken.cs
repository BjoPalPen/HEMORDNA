using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Authentication;

/// <summary>
/// Exchanges a presented refresh token for a new one in the same chain - the operation behind
/// <c>POST /api/auth/refresh</c>.
/// </summary>
public sealed class RotateRefreshToken
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public RotateRefreshToken(IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
    {
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Returns the next token in the chain, or <c>null</c> for every way
    /// <paramref name="rawToken"/> can fail to be usable: unknown, expired, already revoked,
    /// already consumed (reuse), or a losing side of a concurrent rotation of the very same
    /// token. The caller (the API layer) must answer identically - the same 401 - for every
    /// <c>null</c>, exactly the uppdragets säkerhetskrav: "ogiltig, förbrukad, utgången och
    /// okänd ... samma svar, utan att avslöja vilket av fallen det var".
    /// </summary>
    /// <remarks>
    /// Reuse detection: if <paramref name="rawToken"/> was already consumed by an earlier,
    /// completed rotation, or already revoked, this call revokes the whole chain
    /// (<see cref="IRefreshTokenRepository.RevokeChainAsync"/>) before returning <c>null</c> -
    /// including any token that rotation already produced. This codebase cannot tell a stolen
    /// token being replayed apart from two of the legitimate client's own requests racing each
    /// other, so it does not try: both are treated as reuse, and losing a rotation race has the
    /// same consequence as a genuine theft would. That trade-off is deliberate (the alternative
    /// is a rotation scheme reuse detection cannot actually catch) and is why
    /// <see cref="IRefreshTokenRepository.TryConsumeAsync"/> must be a single atomic operation:
    /// it is what decides, for any given token, which one request (if any) gets to be "the one
    /// that rotated it".
    /// </remarks>
    public async Task<IssuedRefreshToken?> HandleAsync(
        string rawToken, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var hash = RefreshTokenSecret.Hash(rawToken);
        var existing = await _refreshTokens.FindByHashAsync(hash, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.RevokedAt is not null)
        {
            return null;
        }

        if (existing.ConsumedAt is not null)
        {
            // Reuse of an already-rotated token - see the remarks above.
            await _refreshTokens.RevokeChainAsync(existing.ChainId, now, cancellationToken);
            return null;
        }

        if (!existing.IsActive(now))
        {
            // Neither consumed nor revoked, so this is a plain, unremarkable expiry - not a
            // theft signal, so nothing else needs revoking.
            return null;
        }

        var wonTheRace = await _refreshTokens.TryConsumeAsync(existing.Id, now, cancellationToken);
        if (!wonTheRace)
        {
            // Another request consumed this exact token between our read above and this call -
            // a concurrent rotation of the same token. See the remarks on this method.
            await _refreshTokens.RevokeChainAsync(existing.ChainId, now, cancellationToken);
            return null;
        }

        var nextSecret = RefreshTokenSecret.Generate();
        var next = RefreshToken.IssueNext(
            existing.ChainId, existing.UserId, RefreshTokenSecret.Hash(nextSecret), now, now + lifetime);

        await _refreshTokens.AddAsync(next, cancellationToken);

        return new IssuedRefreshToken(nextSecret, next.ExpiresAt);
    }
}
