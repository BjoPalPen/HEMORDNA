using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Authentication;

/// <summary>The persistence operations the refresh-token use cases need.</summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>The token matching this hash, or <c>null</c> if none exists. Every caller must
    /// treat <c>null</c> exactly like an inactive token it did find - see
    /// <see cref="RotateRefreshToken"/> and <see cref="RevokeRefreshTokenChain"/>.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically marks the token identified by <paramref name="tokenId"/> as consumed at
    /// <paramref name="now"/>, but only if it was not already consumed or revoked, and reports
    /// whether this call was the one that did it.
    /// </summary>
    /// <remarks>
    /// This is the operation <see cref="RotateRefreshToken"/> relies on to make rotation
    /// atomic: when two requests present the same token at once, at most one of them may be
    /// the caller that flips it from "not yet used" to "consumed" - the loser must learn that
    /// it lost, not silently succeed a second time. A real implementation backing this with a
    /// database must give the check ("not already consumed or revoked") and the write
    /// (setting <c>ConsumedAt</c>) the same atomicity a single conditional
    /// <c>UPDATE ... WHERE "ConsumedAt" IS NULL AND "RevokedAt" IS NULL</c> gives for free in
    /// PostgreSQL (via EF Core's <c>ExecuteUpdate</c>, checking the affected row count) -
    /// a separate read followed by a separate write is not enough, since another request could
    /// run its own write in between.
    /// </remarks>
    Task<bool> TryConsumeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes every token that shares <paramref name="chainId"/>, including ones already
    /// consumed or already revoked - see <see cref="RefreshToken.Revoke"/>. This is how reuse
    /// detection and logout both invalidate a whole rotation chain in one call rather than only
    /// the single token that was presented.
    /// </summary>
    Task RevokeChainAsync(Guid chainId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Revokes every refresh token belonging to <paramref name="userId"/>, across every
    /// chain - used when the user changes their password.</summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}
