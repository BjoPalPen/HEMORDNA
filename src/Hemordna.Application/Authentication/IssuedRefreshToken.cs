namespace Hemordna.Application.Authentication;

/// <summary>
/// A freshly issued or rotated refresh token's owner, raw value and expiry. <see cref="Token"/>
/// exists outside the client that receives it only for the duration of this call - see
/// <see cref="RefreshTokenSecret.Generate"/> for why nothing but this record ever holds it.
/// <see cref="UserId"/> is included so the caller (the API layer) can mint a matching access
/// token in the same response without a second lookup.
/// </summary>
public sealed record IssuedRefreshToken(Guid UserId, string Token, DateTimeOffset ExpiresAt);
