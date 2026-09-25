using Hemordna.Domain.Authentication;
using Hemordna.Domain.Common;

namespace Hemordna.Domain.Tests;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = CreatedAt.AddDays(60);

    // Stand-ins for a lower-case hex-encoded SHA-256 digest, built to exactly
    // RefreshToken.TokenHashLength so a miscounted literal cannot slip in. The real value is
    // produced by hashing a random secret in Commit 2; only the shape matters here.
    private static readonly string TokenHash = new('a', RefreshToken.TokenHashLength);
    private static readonly string OtherTokenHash = new('b', RefreshToken.TokenHashLength);

    [Fact]
    public void IssueNew_starts_a_fresh_chain()
    {
        var userId = Guid.NewGuid();

        var token = RefreshToken.IssueNew(userId, TokenHash, CreatedAt, ExpiresAt);

        Assert.NotEqual(Guid.Empty, token.Id);
        Assert.NotEqual(Guid.Empty, token.ChainId);
        Assert.Equal(userId, token.UserId);
        Assert.Equal(TokenHash, token.TokenHash);
        Assert.Equal(CreatedAt, token.CreatedAt);
        Assert.Equal(ExpiresAt, token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void IssueNew_gives_two_tokens_different_chains()
    {
        var userId = Guid.NewGuid();

        var first = RefreshToken.IssueNew(userId, TokenHash, CreatedAt, ExpiresAt);
        var second = RefreshToken.IssueNew(userId, OtherTokenHash, CreatedAt, ExpiresAt);

        Assert.NotEqual(first.ChainId, second.ChainId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void IssueNew_rejects_an_empty_user_id()
        => Assert.Throws<ArgumentException>(
            () => RefreshToken.IssueNew(Guid.Empty, TokenHash, CreatedAt, ExpiresAt));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IssueNew_rejects_a_blank_token_hash(string? hash)
        => Assert.Throws<ArgumentException>(
            () => RefreshToken.IssueNew(Guid.NewGuid(), hash!, CreatedAt, ExpiresAt));

    [Fact]
    public void IssueNew_rejects_a_token_hash_that_is_too_short()
        => Assert.Throws<ArgumentException>(() => RefreshToken.IssueNew(
            Guid.NewGuid(), new string('a', RefreshToken.TokenHashLength - 1), CreatedAt, ExpiresAt));

    [Fact]
    public void IssueNew_rejects_a_token_hash_that_is_too_long()
        => Assert.Throws<ArgumentException>(() => RefreshToken.IssueNew(
            Guid.NewGuid(), new string('a', RefreshToken.TokenHashLength + 1), CreatedAt, ExpiresAt));

    [Fact]
    public void IssueNew_rejects_an_expiry_at_or_before_creation()
    {
        Assert.Throws<DomainException>(
            () => RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, CreatedAt));
        Assert.Throws<DomainException>(
            () => RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, CreatedAt.AddSeconds(-1)));
    }

    [Fact]
    public void IssueNext_carries_the_chain_id_forward()
    {
        var userId = Guid.NewGuid();
        var original = RefreshToken.IssueNew(userId, TokenHash, CreatedAt, ExpiresAt);
        var rotatedCreatedAt = CreatedAt.AddDays(1);
        var rotatedExpiresAt = rotatedCreatedAt.AddDays(60);

        var next = RefreshToken.IssueNext(
            original.ChainId, userId, OtherTokenHash, rotatedCreatedAt, rotatedExpiresAt);

        Assert.Equal(original.ChainId, next.ChainId);
        Assert.NotEqual(original.Id, next.Id);
        Assert.Equal(userId, next.UserId);
        Assert.Equal(OtherTokenHash, next.TokenHash);
        Assert.Equal(rotatedCreatedAt, next.CreatedAt);
        Assert.Equal(rotatedExpiresAt, next.ExpiresAt);
        Assert.Null(next.ConsumedAt);
        Assert.Null(next.RevokedAt);
    }

    [Fact]
    public void IssueNext_rejects_an_empty_chain_id()
        => Assert.Throws<ArgumentException>(
            () => RefreshToken.IssueNext(Guid.Empty, Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt));

    [Fact]
    public void A_fresh_token_is_active_before_its_expiry()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);

        Assert.True(token.IsActive(ExpiresAt.AddSeconds(-1)));
    }

    [Fact]
    public void A_token_is_not_active_at_or_after_its_expiry()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);

        Assert.False(token.IsActive(ExpiresAt));
        Assert.False(token.IsActive(ExpiresAt.AddSeconds(1)));
    }

    [Fact]
    public void Consume_marks_the_token_consumed_and_inactive()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        var consumedAt = CreatedAt.AddMinutes(5);

        token.Consume(consumedAt);

        Assert.Equal(consumedAt, token.ConsumedAt);
        Assert.False(token.IsActive(consumedAt));
    }

    [Fact]
    public void Consume_twice_throws_a_domain_exception()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        token.Consume(CreatedAt.AddMinutes(5));

        var exception = Assert.Throws<DomainException>(() => token.Consume(CreatedAt.AddMinutes(10)));
        Assert.Equal("This refresh token has already been consumed.", exception.Message);
    }

    [Fact]
    public void Consuming_a_revoked_token_throws_a_domain_exception()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        token.Revoke(CreatedAt.AddMinutes(5));

        var exception = Assert.Throws<DomainException>(() => token.Consume(CreatedAt.AddMinutes(10)));
        Assert.Equal("This refresh token has been revoked and cannot be consumed.", exception.Message);
    }

    [Fact]
    public void Revoke_marks_the_token_inactive()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        var revokedAt = CreatedAt.AddMinutes(5);

        token.Revoke(revokedAt);

        Assert.Equal(revokedAt, token.RevokedAt);
        Assert.False(token.IsActive(revokedAt));
    }

    [Fact]
    public void Revoke_is_idempotent_and_keeps_the_first_timestamp()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        var firstRevoke = CreatedAt.AddMinutes(5);

        token.Revoke(firstRevoke);
        token.Revoke(firstRevoke.AddMinutes(10));

        Assert.Equal(firstRevoke, token.RevokedAt);
    }

    [Fact]
    public void Revoke_after_consume_is_allowed_and_does_not_clear_consumed_at()
    {
        var token = RefreshToken.IssueNew(Guid.NewGuid(), TokenHash, CreatedAt, ExpiresAt);
        var consumedAt = CreatedAt.AddMinutes(5);
        var revokedAt = CreatedAt.AddMinutes(6);

        token.Consume(consumedAt);
        token.Revoke(revokedAt);

        Assert.Equal(consumedAt, token.ConsumedAt);
        Assert.Equal(revokedAt, token.RevokedAt);
    }
}
