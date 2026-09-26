using Hemordna.Application.Authentication;
using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Tests.Authentication;

/// <summary>
/// A fake backed by a plain dictionary and a single lock, standing in for a real database's
/// transactional guarantees. <see cref="TryConsumeAsync"/> in particular must give the same
/// all-or-nothing guarantee a real implementation would give via a conditional
/// <c>UPDATE ... WHERE</c> - see <see cref="IRefreshTokenRepository.TryConsumeAsync"/>'s remarks.
/// </summary>
internal class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, RefreshToken> _byId = [];

    internal IReadOnlyCollection<RefreshToken> All
    {
        get { lock (_gate) { return [.. _byId.Values]; } }
    }

    public virtual Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _byId[token.Id] = token;
        }

        return Task.CompletedTask;
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult(_byId.Values.FirstOrDefault(token => token.TokenHash == tokenHash));
        }
    }

    public virtual Task<bool> TryConsumeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_byId.TryGetValue(tokenId, out var token) || token.ConsumedAt is not null || token.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            token.Consume(now);
            return Task.FromResult(true);
        }
    }

    public Task RevokeChainAsync(Guid chainId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var token in _byId.Values.Where(token => token.ChainId == chainId))
            {
                token.Revoke(now);
            }
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            foreach (var token in _byId.Values.Where(token => token.UserId == userId))
            {
                token.Revoke(now);
            }
        }

        return Task.CompletedTask;
    }
}
