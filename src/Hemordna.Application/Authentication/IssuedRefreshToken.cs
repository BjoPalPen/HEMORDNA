namespace Hemordna.Application.Authentication;

/// <summary>A freshly issued or rotated refresh token's raw value and expiry - the only moment
/// the raw value exists outside the client that receives it. See
/// <see cref="RefreshTokenSecret.Generate"/> for why nothing but this record ever holds it.</summary>
public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);
