using Hemordna.Application.Authentication;
using Hemordna.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly HemordnaDbContext _dbContext;

    public RefreshTokenRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        await _dbContext.RefreshTokens.AddAsync(token, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// <c>AsNoTracking</c> is not just a read-only optimization here (compare
    /// <see cref="ReminderRepository.ListForMemberInRangeAsync"/>): it is required for
    /// correctness. <see cref="RotateRefreshToken"/> calls this method twice within one request -
    /// once before rotating, once again after inserting the next token, specifically to observe
    /// any concurrent revocation that happened in between. Every mutation in this repository goes
    /// through <c>ExecuteUpdateAsync</c>, which writes straight to the database and never touches
    /// the change tracker, so a tracked entity from the first call would keep showing its
    /// original, stale property values forever - the second call would silently hand back the
    /// exact same in-memory object instead of a fresh read, and the concurrent-revocation check
    /// it exists for would never see anything. This was caught by
    /// <c>Hemordna.E2E.Tests.RefreshTokenTests</c> against the real database - the equivalent
    /// in-memory fake in Hemordna.Application.Tests has no such tracking and could not have
    /// caught it.
    /// </summary>
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
        => _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    /// <summary>
    /// A single conditional <c>UPDATE ... WHERE "ConsumedAt" IS NULL AND "RevokedAt" IS NULL</c>
    /// via EF Core's <c>ExecuteUpdateAsync</c> - not a <c>SELECT</c> followed by a separate
    /// <c>SaveChangesAsync</c>. PostgreSQL executes that single statement atomically, so when two
    /// requests race to consume the same token, at most one <c>UPDATE</c> can match the
    /// still-"not consumed" row and change it; the other's <c>WHERE</c> clause simply matches
    /// zero rows. The affected-row count is exactly that verdict, with no separate read to go
    /// stale in between. Verified against the real dev database with EF's command logging - see
    /// this class's remarks and the commit report for the captured SQL text.
    /// </summary>
    public async Task<bool> TryConsumeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.RefreshTokens
            .Where(token => token.Id == tokenId && token.ConsumedAt == null && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.ConsumedAt, now),
                cancellationToken);

        return affectedRows == 1;
    }

    public Task RevokeChainAsync(Guid chainId, DateTimeOffset now, CancellationToken cancellationToken)
        => _dbContext.RefreshTokens
            .Where(token => token.ChainId == chainId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        => _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
}
