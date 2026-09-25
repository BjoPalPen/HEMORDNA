using Hemordna.Application.Authentication;
using Hemordna.Application.Tests.Households;

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
    public async Task Two_concurrent_rotations_of_the_same_token_only_one_succeeds()
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

        Assert.Single(results, result => result is not null);
        Assert.Single(results, result => result is null);
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
}
