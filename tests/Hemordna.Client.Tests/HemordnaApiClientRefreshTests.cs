using System.Net;
using System.Net.Http.Json;
using Hemordna.Client.Contracts;
using Hemordna.Client.Services;
using Microsoft.JSInterop;

namespace Hemordna.Client.Tests;

/// <summary>
/// The single most important test in the refresh-token change (the coordinator's own words):
/// several requests noticing a missing access token at once must never turn into several
/// concurrent presentations of the same refresh token. With rotation, a second presentation of
/// an already-consumed token is treated as theft and revokes the whole chain - so a broken lock
/// here would make the app log people out MORE often than before the change, not less. This
/// cannot be exercised via E2E (CLAUDE.md §8): forcing the real access token to be missing at a
/// precise, repeatable moment across several truly concurrent requests is not something the E2E
/// fixture can stage deterministically, whereas a fake <see cref="HttpMessageHandler"/> can count
/// exactly how many real HTTP calls a burst of concurrent requests produced. No Blazor
/// dependency, no rendered component, no bUnit - a plain HttpClient/IJSRuntime double against the
/// actual <see cref="HemordnaApiClient"/> and <see cref="TokenStore"/> classes.
/// </summary>
public class HemordnaApiClientRefreshTests
{
    [Fact]
    public async Task Several_requests_missing_an_access_token_at_once_trigger_only_one_refresh_call()
    {
        var handler = new FakeApiHandler(seedRefreshToken: "seed-refresh-token");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://hemordna.test/") };
        var tokens = new TokenStore(new FakeJsRuntime(("hemordna.refresh", "seed-refresh-token")));
        var api = new HemordnaApiClient(http, tokens);

        // Four calls fired without individually awaiting any of them first - genuinely
        // concurrent from HemordnaApiClient's point of view, each independently discovering
        // there is no access token in memory yet (a fresh page load: TokenStore never persists
        // the access token - see its own remarks).
        var first = api.GetMeAsync();
        var second = api.GetHouseholdAsync(Guid.NewGuid());
        var third = api.GetMeAsync();
        var fourth = api.GetHouseholdAsync(Guid.NewGuid());

        await Task.WhenAll(first, second, third, fourth);

        Assert.Equal(1, handler.RefreshCallCount);
        // Every request that raced to find a usable access token got one, from the single
        // refresh - none of them were rejected as if the session had failed.
        Assert.NotNull(await first);
    }

    [Fact]
    public async Task A_dead_refresh_token_is_only_ever_presented_once()
    {
        // The other half of the same guarantee: if the refresh token itself is unusable, a burst
        // of concurrent requests must not each try it - that would be four presentations of an
        // already-known-bad token instead of one, for no benefit (see the class remarks - this
        // fake fails EVERY refresh attempt, standing in for a revoked/expired refresh token).
        var handler = new FakeApiHandler(seedRefreshToken: "seed-refresh-token", refreshAlwaysFails: true);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://hemordna.test/") };
        var tokens = new TokenStore(new FakeJsRuntime(("hemordna.refresh", "seed-refresh-token")));
        var api = new HemordnaApiClient(http, tokens);

        var results = await Task.WhenAll(api.GetMeAsync(), api.GetMeAsync(), api.GetMeAsync());

        Assert.Equal(1, handler.RefreshCallCount);
        Assert.All(results, Assert.Null);
    }

    /// <summary>
    /// Simulates the real API closely enough for these tests: <c>/api/auth/refresh</c> mints a
    /// new access token (and counts how many times it was actually called), and any other
    /// endpoint answers 200 only when the caller's bearer token is the current one, 401
    /// otherwise - exactly the shape <see cref="HemordnaApiClient.SendAsync"/> reacts to.
    /// </summary>
    private sealed class FakeApiHandler(string seedRefreshToken, bool refreshAlwaysFails = false) : HttpMessageHandler
    {
        private readonly Lock _gate = new();
        private string? _currentAccessToken;
        private string _currentRefreshToken = seedRefreshToken;

        public int RefreshCallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // A tiny delay widens the window for the four calls above to genuinely overlap on
            // the thread pool, the same reasoning as the Barrier in the server-side concurrency
            // test (RotateRefreshTokenTests) - here a delay is enough since there is no atomic
            // operation on the other end to force a deterministic race against.
            await Task.Delay(5, cancellationToken);

            var path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/api/auth/refresh", StringComparison.Ordinal))
            {
                return await HandleRefreshAsync(request, cancellationToken);
            }

            string? presented = request.Headers.Authorization?.Parameter;
            lock (_gate)
            {
                if (presented is not null && presented == _currentAccessToken)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new MeResponse(Guid.NewGuid(), "test@example.com", "Test", null, null, false))
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        private async Task<HttpResponseMessage> HandleRefreshAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadFromJsonAsync<RefreshRequestBody>(cancellationToken);

            lock (_gate)
            {
                RefreshCallCount++;

                // Only the CURRENT refresh token may succeed - a second presentation of one
                // already consumed by an earlier call in this same test must fail, mirroring
                // real rotation's reuse detection.
                if (refreshAlwaysFails || body?.RefreshToken != _currentRefreshToken)
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                }

                var nextAccessToken = $"access-{RefreshCallCount}";
                var nextRefreshToken = $"refresh-{RefreshCallCount}";
                _currentAccessToken = nextAccessToken;
                _currentRefreshToken = nextRefreshToken;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AccessTokenResponse(
                        nextAccessToken, DateTimeOffset.UtcNow.AddMinutes(30), nextRefreshToken, DateTimeOffset.UtcNow.AddDays(60)))
                };
            }
        }

        private sealed record RefreshRequestBody(string? RefreshToken);
    }

    /// <summary>A minimal <see cref="IJSRuntime"/> standing in for the browser's
    /// <c>localStorage</c>, seeded with whatever key/value pairs the test needs already
    /// present.</summary>
    private sealed class FakeJsRuntime(params (string Key, string Value)[] seed) : IJSRuntime
    {
        private readonly Dictionary<string, string> _storage = seed.ToDictionary(pair => pair.Key, pair => pair.Value);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "localStorage.getItem":
                    _storage.TryGetValue((string)args![0]!, out var value);
                    return ValueTask.FromResult((TValue)(object?)value!);

                case "localStorage.setItem":
                    _storage[(string)args![0]!] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);

                case "localStorage.removeItem":
                    _storage.Remove((string)args![0]!);
                    return ValueTask.FromResult(default(TValue)!);

                default:
                    throw new NotSupportedException($"Unexpected JS call: {identifier}");
            }
        }
    }
}
