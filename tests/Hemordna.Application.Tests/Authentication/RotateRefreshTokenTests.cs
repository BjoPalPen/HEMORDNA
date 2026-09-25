using Hemordna.Application.Authentication;
using Hemordna.Application.Tests.Households;
using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Tests.Authentication;

public class RotateRefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    private readonly InMemoryRefreshTokenRepository _refreshTokens = new();

    private RotateRefreshToken CreateUseCase(DateTimeOffset now) => new(_refreshTokens, new FixedTimeProvider(now));

    private Task<IssuedRefreshToken> IssueAsync(Guid userId, DateTimeOffset now)
        => new IssueRefreshToken(_refreshTokens, new FixedTimeProvider(now))
            .HandleAsync(userId, Lifetime, CancellationToken.None);

    [Fact]
    public async Task Rotation_returns_a_new_token_and_the_old_one_stops_working()
    {
        var userId = Guid.NewGuid();
        var original = await IssueAsync(userId, Now);
        var rotateAt = Now.AddDays(1);

        var rotated = await CreateUseCase(rotateAt).HandleAsync(original.Token, Lifetime, CancellationToken.None);

        Assert.NotNull(rotated);
        Assert.NotEqual(original.Token, rotated.Token);
        Assert.Equal(rotateAt + Lifetime, rotated.ExpiresAt);

        // The old token no longer works - presenting it again is exactly the reuse case, so
        // asserting "rejected" here is left to the dedicated reuse test below. Here it is enough
        // to see its persisted state changed.
        var oldToken = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(original.Token), CancellationToken.None);
        Assert.NotNull(oldToken);
        Assert.False(oldToken.IsActive(rotateAt));
    }

    [Fact]
    public async Task Rotation_carries_the_chain_forward()
    {
        var userId = Guid.NewGuid();
        var original = await IssueAsync(userId, Now);
        var originalToken = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(original.Token), CancellationToken.None);

        var rotated = await CreateUseCase(Now.AddDays(1)).HandleAsync(original.Token, Lifetime, CancellationToken.None);

        var newToken = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(rotated!.Token), CancellationToken.None);
        Assert.Equal(originalToken!.ChainId, newToken!.ChainId);
    }

    [Fact]
    public async Task Reusing_an_already_rotated_token_revokes_the_whole_chain_including_the_token_it_produced()
    {
        var userId = Guid.NewGuid();
        var original = await IssueAsync(userId, Now);
        var firstRotation = Now.AddDays(1);
        var rotated = await CreateUseCase(firstRotation).HandleAsync(original.Token, Lifetime, CancellationToken.None);
        Assert.NotNull(rotated);

        // The old token is presented again - a stolen copy being replayed, or a client that
        // (wrongly) kept using it after already rotating once.
        var reuseAttemptAt = firstRotation.AddMinutes(5);
        var reuseResult = await CreateUseCase(reuseAttemptAt)
            .HandleAsync(original.Token, Lifetime, CancellationToken.None);

        Assert.Null(reuseResult);

        // The whole point of the rule: the token that rotation legitimately produced must ALSO
        // stop working now, not just the one that was reused.
        var laterRotationAttempt = await CreateUseCase(reuseAttemptAt.AddMinutes(1))
            .HandleAsync(rotated.Token, Lifetime, CancellationToken.None);
        Assert.Null(laterRotationAttempt);
    }

    [Fact]
    public async Task An_expired_token_is_rejected_without_revoking_the_chain()
    {
        var userId = Guid.NewGuid();
        var original = await IssueAsync(userId, Now);
        var afterExpiry = Now + Lifetime + TimeSpan.FromMinutes(1);

        var result = await CreateUseCase(afterExpiry).HandleAsync(original.Token, Lifetime, CancellationToken.None);

        Assert.Null(result);

        // Plain expiry is not a theft signal - nothing else in the chain should be touched.
        var token = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(original.Token), CancellationToken.None);
        Assert.Null(token!.RevokedAt);
    }

    [Fact]
    public async Task A_revoked_token_is_rejected()
    {
        var userId = Guid.NewGuid();
        var original = await IssueAsync(userId, Now);
        var token = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(original.Token), CancellationToken.None);
        token!.Revoke(Now.AddHours(1));

        var result = await CreateUseCase(Now.AddHours(2)).HandleAsync(original.Token, Lifetime, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected()
    {
        var result = await CreateUseCase(Now).HandleAsync("not-a-real-token", Lifetime, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Rotating_one_users_token_never_touches_another_users_token()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var tokenA = await IssueAsync(userA, Now);
        var tokenB = await IssueAsync(userB, Now);

        await CreateUseCase(Now.AddDays(1)).HandleAsync(tokenA.Token, Lifetime, CancellationToken.None);

        var stillTokenB = await _refreshTokens.FindByHashAsync(RefreshTokenSecret.Hash(tokenB.Token), CancellationToken.None);
        Assert.NotNull(stillTokenB);
        Assert.True(stillTokenB.IsActive(Now.AddDays(1)));
    }

    [Fact]
    public async Task Two_concurrent_rotations_of_the_same_token_never_both_succeed()
    {
        // A Barrier forces both calls to reach the atomic "consume" step at the same instant,
        // regardless of how the thread pool happens to schedule them - without it, two awaited
        // calls could simply run one after another and the test would prove nothing about real
        // concurrency (see the coordinator's note: "inte sekventiellt med ett hoppfullt namn").
        var barrier = new Barrier(2);
        var repository = new BarrierSynchronizedRefreshTokenRepository(barrier);
        var userId = Guid.NewGuid();
        var issued = await new IssueRefreshToken(repository, new FixedTimeProvider(Now))
            .HandleAsync(userId, Lifetime, CancellationToken.None);

        Task<IssuedRefreshToken?> RotateAsync()
            => new RotateRefreshToken(repository, new FixedTimeProvider(Now.AddDays(1)))
                .HandleAsync(issued.Token, Lifetime, CancellationToken.None);

        var first = Task.Run(RotateAsync);
        var second = Task.Run(RotateAsync);
        var results = await Task.WhenAll(first, second);

        // The one guarantee that actually matters for security: two concurrent presentations of
        // the same token can never BOTH walk away with a working new token - that would mean the
        // same secret was rotated twice. At most one may succeed. On rare adversarial timing (a
        // losing side's chain revocation landing exactly between the winning side's own insert
        // and its subsequent re-check - see RotateRefreshToken's remarks on that re-check) BOTH
        // may instead fail, which is safe even if not ideal: the caller has to sign in again,
        // rather than either side having kept a token alive that reuse detection should have
        // killed. An earlier version of this test asserted exactly one success always, which
        // this fake occasionally disproved once the re-check existed - see the commit report.
        Assert.True(
            results.Count(result => result is not null) <= 1,
            $"Expected at most one success, got: [{string.Join(", ", results)}]");

        // Whatever the outcome, at most one token to come out of this chain is left active -
        // never two live tokens minted from racing the same rotation.
        Assert.True(repository.All.Count(token => token.IsActive(Now.AddDays(1))) <= 1);
    }

    [Fact]
    public async Task A_chain_revoked_by_a_racing_caller_between_this_calls_own_insert_and_return_still_loses_its_new_token()
    {
        // Deterministic version of the race the Barrier test above only exercises
        // probabilistically: a concurrent caller could revoke this exact chain in the narrow
        // window between this rotation's own AddAsync and its return - see the remarks on
        // RotateRefreshToken.HandleAsync for why the re-check after AddAsync exists at all. This
        // fake simulates that window deterministically by revoking the chain itself, from inside
        // AddAsync, immediately after the new token is inserted but before HandleAsync re-checks.
        var userId = Guid.NewGuid();
        var repository = new RevokingWhileInsertingRefreshTokenRepository();
        var original = await new IssueRefreshToken(repository, new FixedTimeProvider(Now))
            .HandleAsync(userId, Lifetime, CancellationToken.None);

        var result = await new RotateRefreshToken(repository, new FixedTimeProvider(Now.AddDays(1)))
            .HandleAsync(original.Token, Lifetime, CancellationToken.None);

        Assert.Null(result);
        Assert.All(repository.All, token => Assert.False(token.IsActive(Now.AddDays(1))));
    }

    /// <summary>
    /// A test-only wrapper that forces two concurrent <see cref="TryConsumeAsync"/> calls to both
    /// reach the critical section before either is allowed past it, so the atomicity test above
    /// exercises a genuine race every time it runs rather than relying on scheduling luck.
    /// </summary>
    private sealed class BarrierSynchronizedRefreshTokenRepository(Barrier barrier) : InMemoryRefreshTokenRepository
    {
        public override async Task<bool> TryConsumeAsync(Guid tokenId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            barrier.SignalAndWait(cancellationToken);
            return await base.TryConsumeAsync(tokenId, now, cancellationToken);
        }
    }

    /// <summary>
    /// Simulates a racing caller's chain revocation landing exactly between this rotation's own
    /// insert and its subsequent re-check - deterministically, instead of hoping real thread
    /// scheduling produces that interleaving. See the test above that uses this.
    /// </summary>
    private sealed class RevokingWhileInsertingRefreshTokenRepository : InMemoryRefreshTokenRepository
    {
        public override async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
        {
            await base.AddAsync(token, cancellationToken);

            // Only once a chain already has a sibling - never for the very first token a chain
            // starts with (also inserted through this method, by IssueRefreshToken) - simulates a
            // concurrent loser's RevokeChainAsync landing right after this rotation's own insert.
            if (All.Count(sibling => sibling.ChainId == token.ChainId) > 1)
            {
                await RevokeChainAsync(token.ChainId, token.CreatedAt, cancellationToken);
            }
        }
    }
}
