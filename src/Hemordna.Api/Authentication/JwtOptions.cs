namespace Hemordna.Api.Authentication;

/// <summary>
/// JWT signing configuration, bound from the <c>Jwt</c> configuration section.
/// The signing key is a secret and never has a default - it comes from the environment.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length in bytes. HS256 keys shorter than this are rejected.</summary>
    public const int MinimumSigningKeyBytes = 32;

    public string Issuer { get; set; } = "hemordna";

    public string Audience { get; set; } = "hemordna";

    /// <summary>The HMAC signing key. Must be supplied through configuration or environment.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// 30 minutes, by decision - short because a leaked access token cannot be revoked before it
    /// expires (unlike a refresh token - see <see cref="RefreshTokenLifetimeDays"/>), and silent
    /// renewal (<c>POST /api/auth/refresh</c>) makes the exact length invisible to the person
    /// using the app either way.
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 30;

    /// <summary>
    /// 60 days, by decision, refreshed on every rotation - someone who opens the app at least
    /// once a month is never asked to sign in again. Unlike an access token, a refresh token can
    /// be revoked (rotation with reuse detection, logout, password change), which is what makes
    /// a lifetime this long acceptable.
    /// </summary>
    public int RefreshTokenLifetimeDays { get; set; } = 60;

    /// <summary>Fails fast at startup rather than issuing tokens nobody can trust.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            throw new InvalidOperationException(
                $"'{SectionName}:SigningKey' is not configured. Set Jwt__SigningKey in the environment.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(SigningKey) < MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:SigningKey' must be at least {MinimumSigningKeyBytes} bytes for HMAC-SHA256.");
        }

        if (TokenLifetimeMinutes <= 0)
        {
            throw new InvalidOperationException($"'{SectionName}:TokenLifetimeMinutes' must be positive.");
        }

        if (RefreshTokenLifetimeDays <= 0)
        {
            throw new InvalidOperationException($"'{SectionName}:RefreshTokenLifetimeDays' must be positive.");
        }
    }
}
