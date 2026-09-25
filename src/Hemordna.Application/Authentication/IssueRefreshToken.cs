using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Authentication;

/// <summary>Starts a brand-new refresh-token chain, e.g. at login, registration or a successful
/// passkey sign-in.</summary>
public sealed class IssueRefreshToken
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly TimeProvider _timeProvider;

    public IssueRefreshToken(IRefreshTokenRepository refreshTokens, TimeProvider timeProvider)
    {
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
    }

    /// <summary><paramref name="lifetime"/> is passed in rather than read from configuration
    /// here - the caller (the API layer) owns the actual policy value (60 days, by decision),
    /// and this use case stays testable without needing to know where that number comes
    /// from.</summary>
    public async Task<IssuedRefreshToken> HandleAsync(Guid userId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var secret = RefreshTokenSecret.Generate();
        var token = RefreshToken.IssueNew(userId, RefreshTokenSecret.Hash(secret), now, now + lifetime);

        await _refreshTokens.AddAsync(token, cancellationToken);

        return new IssuedRefreshToken(secret, token.ExpiresAt);
    }
}
