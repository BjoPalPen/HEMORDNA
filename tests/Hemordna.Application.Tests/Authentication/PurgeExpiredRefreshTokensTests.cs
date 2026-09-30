using Hemordna.Application.Authentication;
using Hemordna.Domain.Authentication;

namespace Hemordna.Application.Tests.Authentication;

/// <summary>
/// Gallringen av refresh-tokens. Det testet som betyder mest är
/// <see cref="A_consumed_token_that_has_not_expired_is_kept"/>: en förbrukad token som ännu inte
/// gått ut ÄR återanvändningsdetekteringens minne, och raderas den för tidigt blir ett
/// återspelat stöldförsök en okänd token i stället - avvisad, men utan att kedjan återkallas.
/// </summary>
public class PurgeExpiredRefreshTokensTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryRefreshTokenRepository _tokens = new();

    private PurgeExpiredRefreshTokens CreateUseCase() => new(_tokens);

    private async Task<RefreshToken> SeedAsync(
        DateTimeOffset expiresAt, bool consumed = false, bool revoked = false)
    {
        var token = RefreshToken.IssueNew(
            Guid.NewGuid(), new string('a', RefreshToken.TokenHashLength), Now.AddDays(-90), expiresAt);

        if (consumed)
        {
            token.Consume(Now.AddDays(-89));
        }

        if (revoked)
        {
            token.Revoke(Now.AddDays(-89));
        }

        await _tokens.AddAsync(token, CancellationToken.None);
        return token;
    }

    [Fact]
    public async Task An_expired_token_is_deleted()
    {
        await SeedAsync(expiresAt: Now.AddSeconds(-1));

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Empty(_tokens.All);
    }

    [Fact]
    public async Task A_token_that_is_still_valid_is_kept()
    {
        await SeedAsync(expiresAt: Now.AddDays(30));

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(_tokens.All);
    }

    /// <summary>Gränsfallet: en token som går ut exakt nu har inte gått ut än.</summary>
    [Fact]
    public async Task A_token_expiring_exactly_now_is_kept()
    {
        await SeedAsync(expiresAt: Now);

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(_tokens.All);
    }

    /// <summary>Det här testet skyddar återanvändningsdetekteringen. Raderas en förbrukad men
    /// ännu giltig token blir ett återspelat stöldförsök en okänd token - avvisad, men utan att
    /// hela kedjan återkallas, vilket är hela poängen med rotation.</summary>
    [Fact]
    public async Task A_consumed_token_that_has_not_expired_is_kept()
    {
        await SeedAsync(expiresAt: Now.AddDays(30), consumed: true);

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(_tokens.All);
    }

    /// <summary>En återkallad token bär samma minne som en förbrukad - kedjan kan fortfarande
    /// träffas av ett återspelningsförsök så länge den inte gått ut.</summary>
    [Fact]
    public async Task A_revoked_token_that_has_not_expired_is_kept()
    {
        await SeedAsync(expiresAt: Now.AddDays(30), revoked: true);

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(0, deleted);
        Assert.Single(_tokens.All);
    }

    /// <summary>Efter utgång finns inget beteende kvar att bevara - en utgången token och en
    /// okänd behandlas redan likadant, oavsett vilket tillstånd raden hade.</summary>
    [Fact]
    public async Task An_expired_token_is_deleted_whatever_state_it_was_in()
    {
        await SeedAsync(expiresAt: Now.AddDays(-1));
        await SeedAsync(expiresAt: Now.AddDays(-1), consumed: true);
        await SeedAsync(expiresAt: Now.AddDays(-1), revoked: true);

        var deleted = await CreateUseCase().HandleAsync(Now, CancellationToken.None);

        Assert.Equal(3, deleted);
        Assert.Empty(_tokens.All);
    }
}
