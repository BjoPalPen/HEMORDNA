using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace Hemordna.E2E.Tests;

/// <summary>
/// Gets a usable access token for tests that call the API directly (bypassing the UI) for setup
/// or assertions - the same 41 call sites this replaced used to read the access token straight
/// out of <c>localStorage</c>. That stopped being possible once the client moved the access token
/// to memory (see docs/ARCHITECTURE.md "Beslut: Refresh-token med rotation"): nothing durable to
/// read from the page's storage anymore. What IS still in <c>localStorage</c> is the refresh
/// token, under its own key (<c>hemordna.refresh</c>, deliberately not the old
/// <c>hemordna.token</c> - see TokenStore's remarks), so this exchanges that for a fresh access
/// token the same way the app itself would, via <c>POST /api/auth/refresh</c>.
/// </summary>
/// <remarks>
/// Rotation means the refresh token this reads is consumed the moment it is exchanged - so this
/// writes the newly rotated refresh token straight back into the page's own <c>localStorage</c>
/// before returning. Without that, the browser would be left holding a now-dead refresh token:
/// harmless as long as nothing else in the test ever needs to refresh again, but a real landmine
/// the moment a test reloads the page, runs long enough for the app's own silent refresh to fire,
/// or calls this helper a second time for the same page - any of those would present an
/// already-consumed token, which reuse detection treats as theft and revokes the whole chain,
/// signing the test's own session out for real. Writing the rotated value back keeps the page's
/// stored refresh token perpetually valid regardless of how many times this is called.
/// </remarks>
internal static class AccessTokenHelper
{
    internal static async Task<string> GetAsync(IPage page, string apiBaseUrl)
    {
        var refreshToken = await page.EvaluateAsync<string>("() => localStorage.getItem('hemordna.refresh')");

        using var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
        using var response = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rotatedRefreshToken = body.GetProperty("refreshToken").GetString();

        await page.EvaluateAsync(
            "([key, value]) => localStorage.setItem(key, value)",
            new object?[] { "hemordna.refresh", rotatedRefreshToken });

        return body.GetProperty("token").GetString()!;
    }
}
