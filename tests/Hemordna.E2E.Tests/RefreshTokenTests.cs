using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hemordna.E2E.Tests;

/// <summary>
/// End-to-end coverage for refresh-token rotation against the real API and the real
/// PostgreSQL database the fixture starts - see docs/ARCHITECTURE.md "Beslut: Refresh-token
/// med rotation". <see cref="Two_concurrent_refreshes_of_the_same_token_never_both_succeed_and_the_chain_ends_up_revoked"/>
/// is the one the atomic <c>RefreshTokenRepository.TryConsumeAsync</c> implementation exists to
/// pass - see its own remarks for why an in-process, in-memory test cannot substitute for this.
/// </summary>
[Collection(HemordnaAppCollection.Name)]
public class RefreshTokenTests
{
    private const string Password = "Refresh-Test-Password-2026!";
    private readonly HemordnaAppFixture _app;

    public RefreshTokenTests(HemordnaAppFixture app) => _app = app;

    private async Task<(HttpClient Http, string AccessToken, string RefreshToken)> RegisterAsync()
    {
        var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        var email = $"e2e-refresh-{Guid.NewGuid():N}@example.com";
        using var response = await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, displayName = "Refresh" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (http, body.GetProperty("token").GetString()!, body.GetProperty("refreshToken").GetString()!);
    }

    [Fact]
    public async Task Register_issues_a_refresh_token_alongside_the_access_token()
    {
        var (http, _, refreshToken) = await RegisterAsync();
        using (http)
        {
            Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        }
    }

    [Fact]
    public async Task Refreshing_returns_a_working_new_access_token_and_a_new_refresh_token()
    {
        var (http, _, refreshToken) = await RegisterAsync();
        using (http)
        {
            using var refreshed = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

            var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
            var newAccessToken = body.GetProperty("token").GetString()!;
            var newRefreshToken = body.GetProperty("refreshToken").GetString()!;
            Assert.NotEqual(refreshToken, newRefreshToken);

            http.DefaultRequestHeaders.Authorization = new("Bearer", newAccessToken);
            using var me = await http.GetAsync("/api/me");
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_rejected()
    {
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };

        using var response = await http.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = "not-a-real-refresh-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reusing_an_already_rotated_refresh_token_revokes_the_whole_chain()
    {
        var (http, _, refreshToken) = await RegisterAsync();
        using (http)
        {
            using var first = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var body = await first.Content.ReadFromJsonAsync<JsonElement>();
            var newRefreshToken = body.GetProperty("refreshToken").GetString()!;

            // The old, already-rotated token is presented again - a stolen copy being replayed,
            // or a client that (wrongly) kept using it after already rotating once.
            using var reuse = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

            // The whole point of the rule: the token the first, legitimate rotation produced
            // must ALSO stop working now, not just the one that was reused.
            using var afterReuse = await http.PostAsJsonAsync(
                "/api/auth/refresh", new { refreshToken = newRefreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
        }
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (http, _, refreshToken) = await RegisterAsync();
        using (http)
        {
            using var loggedOut = await http.PostAsJsonAsync("/api/auth/logout", new { refreshToken });
            Assert.Equal(HttpStatusCode.OK, loggedOut.StatusCode);

            using var refreshed = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);
        }
    }

    [Fact]
    public async Task Password_change_revokes_the_refresh_token()
    {
        // Uses the access token register itself already returned, rather than spending the
        // refresh token on a rotation first - this test needs that refresh token to still be
        // active going into change-password, so the assertion below actually exercises
        // RevokeAllRefreshTokensForUser rather than a token that was already consumed by an
        // earlier call in this same test.
        var (http, accessToken, refreshToken) = await RegisterAsync();
        using (http)
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

            using var changed = await http.PostAsJsonAsync("/api/auth/change-password", new
            {
                currentPassword = Password,
                newPassword = "Refresh-Test-New-Password-2026!"
            });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

            using var refreshAfterChange = await http.PostAsJsonAsync(
                "/api/auth/refresh", new { refreshToken });
            Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterChange.StatusCode);
        }
    }

    [Fact]
    public async Task Password_reset_revokes_the_refresh_token()
    {
        // Reset-password (the forgotten-password flow, via an e-mailed link) is what someone
        // uses when they believe the account has been accessed by somebody else - a refresh
        // token issued before that must not still be usable afterwards, or the one thing the
        // user knew to do about it would leave an attacker up to 60 more days on the account.
        using var http = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        var email = $"e2e-refresh-reset-{Guid.NewGuid():N}@example.com";
        using var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email, password = Password, displayName = "Refresh" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        var refreshToken = (await registered.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("refreshToken").GetString()!;

        using var requested = await http.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        var html = await http.GetStringAsync($"/api/auth/dev/last-email?email={Uri.EscapeDataString(email)}");
        var link = new Uri(WebUtility.HtmlDecode(Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value));
        var query = System.Web.HttpUtility.ParseQueryString(link.Query);

        using var reset = await http.PostAsJsonAsync("/api/auth/reset-password", new
        {
            email,
            token = query["token"],
            newPassword = "Refresh-Test-New-Password-2026!"
        });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using var refreshAfterReset = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterReset.StatusCode);
    }

    /// <summary>
    /// The test the coordinator asked for directly: two genuinely separate HTTP requests, over
    /// real sockets, against the real API process and the real PostgreSQL database - fired
    /// without individually awaiting either first, so this is concurrent because the requests
    /// actually race over the network and in the database, not because the test is merely named
    /// as if it were. This is what
    /// <c>RefreshTokenRepository.TryConsumeAsync</c>'s single conditional <c>UPDATE</c> exists to
    /// make safe - an in-memory fake (see Hemordna.Application.Tests) can prove the USE CASE's
    /// own logic is correct given an atomic repository, but it cannot prove the real EF/Npgsql
    /// implementation actually delivers that atomicity; only hitting the real database can.
    /// </summary>
    /// <remarks>
    /// The only guarantee this asserts unconditionally is the security-critical one: the two
    /// requests can never BOTH walk away with a working token, since that would mean the same
    /// secret was rotated twice. On genuinely simultaneous presentation, both requests reach
    /// their decision (who "won") independently, so on rare adversarial timing BOTH can instead
    /// be rejected (see RotateRefreshToken's remarks on the race between a losing side's chain
    /// revocation and a winning side's own insert) - safe, if not ideal, and not asserted against
    /// here since it is not the property this test exists to guard. When exactly one request
    /// does succeed, the second assertion below is unconditional: the token race handed back must
    /// not outlive the race that produced it.
    /// </remarks>
    [Fact]
    public async Task Two_concurrent_refreshes_of_the_same_token_never_both_succeed_and_the_chain_ends_up_revoked()
    {
        var (http, _, refreshToken) = await RegisterAsync();
        using var secondHttp = new HttpClient { BaseAddress = new Uri(_app.ApiUrl) };
        using (http)
        {
            var firstCall = http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            var secondCall = secondHttp.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            var responses = await Task.WhenAll(firstCall, secondCall);

            try
            {
                var statusCodes = responses.Select(response => response.StatusCode).ToList();
                var successes = statusCodes.Count(code => code == HttpStatusCode.OK);
                var rejections = statusCodes.Count(code => code == HttpStatusCode.Unauthorized);

                Assert.True(
                    successes <= 1,
                    $"Expected at most one 200 among two concurrent refreshes of the same token, " +
                    $"got: [{string.Join(", ", statusCodes)}]");
                Assert.Equal(2, successes + rejections);

                if (successes == 1)
                {
                    var winner = responses[statusCodes.IndexOf(HttpStatusCode.OK)];
                    var winnersToken = (await winner.Content.ReadFromJsonAsync<JsonElement>())
                        .GetProperty("refreshToken").GetString()!;

                    // The requirement this whole test exists to prove: even the token the
                    // successful side of the race handed back must not survive it.
                    using var afterRace = await http.PostAsJsonAsync(
                        "/api/auth/refresh", new { refreshToken = winnersToken });
                    Assert.Equal(HttpStatusCode.Unauthorized, afterRace.StatusCode);
                }
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
    }
}
