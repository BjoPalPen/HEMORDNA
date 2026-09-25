using Hemordna.Domain.Common;

namespace Hemordna.Domain.Authentication;

/// <summary>
/// One link in a refresh-token rotation chain, used to keep a user signed in without issuing a
/// long-lived access token (which cannot be revoked once handed out - see
/// docs/ARCHITECTURE.md "Beslut: Refresh-token med rotation"). Only a hash of the actual token
/// value is ever stored here: <see cref="TokenHash"/> is the SHA-256 digest of the secret the
/// client holds, hex-encoded to <see cref="TokenHashLength"/> characters. A leaked database dump
/// contains no value a client could present to sign in.
/// </summary>
/// <remarks>
/// Like <see cref="Households.HouseholdMember.UserId"/> and
/// <c>Hemordna.Infrastructure.Identity.PasskeyCredential.UserId</c>, <see cref="UserId"/> is a
/// plain <see cref="Guid"/> with no navigation property: the identity user lives in
/// <c>Hemordna.Infrastructure.Identity</c>, outside this layer, and no relational foreign key is
/// configured against it either - see <c>RefreshTokenConfiguration</c>.
/// </remarks>
public sealed class RefreshToken
{
    /// <summary>Length of a lowercase hex-encoded SHA-256 digest (32 bytes -> 64 hex characters).</summary>
    public const int TokenHashLength = 64;

    private RefreshToken(
        Guid id,
        Guid userId,
        Guid chainId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        ChainId = chainId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    /// <summary>The user this token was issued to. See the remarks on this type for why there is
    /// no navigation property.</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Identifies the rotation chain this token belongs to. The token issued at login starts a
    /// new chain (<see cref="IssueNew"/>); every token minted by rotating an existing one keeps
    /// the same value (<see cref="IssueNext"/>). Reuse detection - presenting a token that has
    /// already been consumed or revoked - must revoke every token that shares a
    /// <see cref="ChainId"/>, not only the one presented, since a stolen token could otherwise
    /// still be rotated forward under a sibling that was already re-issued.
    /// </summary>
    public Guid ChainId { get; private set; }

    /// <summary>
    /// SHA-256 hash of the refresh token's secret value, lower-case hex-encoded. The plaintext
    /// value is handed to the client once, at issuance, and is never persisted anywhere.
    /// </summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When this token was exchanged for the next one in its chain via rotation.
    /// <c>null</c> while the token has not yet been used.</summary>
    public DateTimeOffset? ConsumedAt { get; private set; }

    /// <summary>When this token was explicitly invalidated - logout, a password change, or reuse
    /// detection revoking its whole chain. <c>null</c> while the token has not been revoked.
    /// Revoking an already-consumed token is allowed: revoking a chain marks every token in it,
    /// regardless of whether it was already spent, so nothing in the chain can be mistaken for
    /// still-valid later.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Starts a brand-new rotation chain - the first token issued for a sign-in.</summary>
    public static RefreshToken IssueNew(
        Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        => new(
            Guid.NewGuid(),
            Guard.AgainstEmpty(userId, nameof(userId)),
            Guid.NewGuid(),
            ValidateTokenHash(tokenHash),
            createdAt,
            ValidateExpiry(createdAt, expiresAt));

    /// <summary>
    /// Mints the next token in an existing rotation chain. <paramref name="chainId"/> is carried
    /// over unchanged from the token being rotated - see <see cref="ChainId"/>.
    /// </summary>
    public static RefreshToken IssueNext(
        Guid chainId, Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
        => new(
            Guid.NewGuid(),
            Guard.AgainstEmpty(userId, nameof(userId)),
            Guard.AgainstEmpty(chainId, nameof(chainId)),
            ValidateTokenHash(tokenHash),
            createdAt,
            ValidateExpiry(createdAt, expiresAt));

    /// <summary>Whether this token can still be exchanged for a new one at <paramref name="now"/>:
    /// neither consumed nor revoked, and not yet expired.</summary>
    public bool IsActive(DateTimeOffset now)
        => ConsumedAt is null && RevokedAt is null && now < ExpiresAt;

    /// <summary>
    /// Marks this token as exchanged for a new one. A token can only be consumed once: presenting
    /// an already-consumed or already-revoked token again is a reuse attempt, and the caller is
    /// expected to have checked <see cref="IsActive"/> first and, on failure, revoke the whole
    /// chain rather than call this - see the remarks on <see cref="ChainId"/>.
    /// </summary>
    public void Consume(DateTimeOffset now)
    {
        if (ConsumedAt is not null)
        {
            throw new DomainException("This refresh token has already been consumed.");
        }

        if (RevokedAt is not null)
        {
            throw new DomainException("This refresh token has been revoked and cannot be consumed.");
        }

        ConsumedAt = now;
    }

    /// <summary>Revokes this single token. Idempotent - revoking an already-revoked token keeps
    /// its original <see cref="RevokedAt"/> rather than overwriting it, and revoking an
    /// already-consumed token is allowed (see the remarks on <see cref="RevokedAt"/>).</summary>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static string ValidateTokenHash(string tokenHash)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(tokenHash, nameof(tokenHash));

        if (trimmed.Length != TokenHashLength)
        {
            throw new ArgumentException(
                $"Token hash must be exactly {TokenHashLength} characters (a hex-encoded SHA-256 digest).",
                nameof(tokenHash));
        }

        return trimmed;
    }

    private static DateTimeOffset ValidateExpiry(DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        if (expiresAt <= createdAt)
        {
            throw new DomainException("A refresh token cannot expire at or before it is created.");
        }

        return expiresAt;
    }
}
