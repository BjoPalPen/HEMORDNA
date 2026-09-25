namespace Hemordna.Api.Tests;

/// <summary>
/// Covers the Cache-Control headers Program.cs applies to the Blazor app shell - see the
/// SetNoCacheForAppShellFiles local function and the comments above UseStaticFiles and
/// MapFallbackToFile. This is what actually caught the production bug: the same OnPrepareResponse
/// existed on UseStaticFiles alone for a long time, which passes tests 1-5 below on its own, but
/// leaves every SPA route unset - only test 6 (the fallback) can catch that, which is exactly why
/// it exists as its own case rather than being folded into the others.
/// </summary>
public sealed class AppShellCacheHeaderTests : IClassFixture<AppShellTestFactory>
{
    private readonly HttpClient _client;

    public AppShellCacheHeaderTests(AppShellTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/service-worker.js")]
    [InlineData("/service-worker-assets.js")]
    [InlineData("/index.html")]
    [InlineData("/manifest.webmanifest")]
    public async Task AppShellFile_IsServedWithNoCache(string path)
    {
        var response = await _client.GetAsync(path);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoCache, "Expected Cache-Control: no-cache.");
    }

    [Fact]
    public async Task Root_ResolvedThroughUseDefaultFiles_IsServedWithNoCache()
    {
        // "/" takes a different path through the pipeline than a direct "/index.html" request
        // (UseDefaultFiles rewrites the path before UseStaticFiles serves it) - tested
        // separately from the case above on purpose, so a regression in one is never masked by
        // the other happening to still pass.
        var response = await _client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoCache, "Expected Cache-Control: no-cache.");
    }

    [Fact]
    public async Task UnknownClientRoute_FallsBackToIndexWithNoCache()
    {
        // This is the case the production bug actually broke: MapFallbackToFile had no
        // StaticFileOptions of its own, so every unmatched client-side route (e.g. /vecka,
        // /hushall) served index.html with no Cache-Control at all, even after UseStaticFiles
        // itself already had OnPrepareResponse. See AppShellTestFactory's remarks and this
        // file's own summary above.
        var response = await _client.GetAsync("/okand/123");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoCache, "Expected Cache-Control: no-cache.");
    }

    [Fact]
    public async Task FingerprintedFrameworkFile_IsNotServedWithNoCache()
    {
        // The negative case: SetNoCacheForAppShellFiles must not become "every static file gets
        // no-cache" by accident. _framework/* is content-hashed and safe to cache forever - a
        // test that only checked the app-shell files above would still pass if someone later
        // applied no-cache to everything.
        var response = await _client.GetAsync("/_framework/dummy.abc123.wasm");

        response.EnsureSuccessStatusCode();
        Assert.False(response.Headers.CacheControl?.NoCache ?? false, "Did not expect Cache-Control: no-cache.");
    }
}
