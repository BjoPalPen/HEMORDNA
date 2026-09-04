using System.Security.Claims;
using System.Text;
using Hemordna.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hemordna.Api.Authentication;

/// <summary>Issues the access tokens the API accepts.</summary>
/// <remarks>
/// The token carries only who the caller is. Household membership is deliberately left out:
/// a claim would go stale the moment membership changes, so it is resolved per request
/// against the database instead.
/// </remarks>
public sealed class JwtTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public AccessToken Issue(HemordnaUser user)
    {
        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.TokenLifetimeMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
                [ClaimTypes.Name] = user.DisplayName
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }
}

/// <summary>An issued access token and when it stops being valid.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
