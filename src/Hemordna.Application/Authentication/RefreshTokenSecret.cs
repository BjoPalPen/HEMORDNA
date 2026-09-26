using System.Security.Cryptography;
using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Authentication;

/// <summary>
/// Generates the raw refresh-token secret handed to a client, and hashes it the same way for
/// storage and lookup. CLAUDE.md §10: the raw value returned by <see cref="Generate"/> must
/// never be logged, and neither must the hash <see cref="Hash"/> produces - a hash is still a
/// bearer credential for as long as the token it came from is valid.
/// </summary>
public static class RefreshTokenSecret
{
    /// <summary>256 bits of randomness, the same order of magnitude .NET's own
    /// <c>RandomNumberGenerator</c>-backed anti-forgery tokens use.</summary>
    private const int SecretByteLength = 32;

    /// <summary>
    /// A new cryptographically random secret, base64url-encoded (no padding, no characters that
    /// need escaping in a URL or a JSON string). This is the only place the plaintext value
    /// exists outside the client that receives it - it is never persisted; see
    /// <see cref="Hash"/> for what gets stored instead.
    /// </summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// SHA-256 of <paramref name="secret"/>, lower-case hex-encoded to exactly
    /// <see cref="RefreshToken.TokenHashLength"/> characters - matching what
    /// <see cref="RefreshToken.TokenHash"/> expects. Hashing is deterministic (no salt): a
    /// presented token is looked up by hashing it the same way and matching the stored value,
    /// the same reasoning <see cref="RefreshToken"/>'s own remarks give for storing only a hash.
    /// </summary>
    public static string Hash(string secret)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(bytes);
    }
}
