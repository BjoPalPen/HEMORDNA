using Microsoft.JSInterop;

namespace Hemordna.Client.Services;

/// <summary>
/// Holds the access token in memory only - never persisted, so a leaked or stolen browser
/// storage dump contains no usable bearer token (see docs/ARCHITECTURE.md "Beslut: Refresh-token
/// med rotation"). The refresh token is the one thing kept in the browser's local storage, since
/// it has to survive a page reload or the app being relaunched days later; a rotation and reuse
/// detection on the server is what makes that acceptable despite living on disk.
/// </summary>
public sealed class TokenStore
{
    // Deliberately not the old "hemordna.token" key this class itself used before the access
    // token moved to memory - see RemoveLegacyAccessTokenAsync for why that key still matters.
    private const string RefreshTokenStorageKey = "hemordna.refresh";

    private const string LegacyAccessTokenStorageKey = "hemordna.token";

    private readonly IJSRuntime _js;

    public TokenStore(IJSRuntime js) => _js = js;

    /// <summary>The current access token, or <c>null</c> before anything has signed in this page
    /// load. Never read from or written to storage - see this type's remarks.</summary>
    public string? AccessToken { get; private set; }

    public void SetAccessToken(string token) => AccessToken = token;

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenStorageKey);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetRefreshTokenAsync(string token)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenStorageKey, token);
        }
        catch (JSException)
        {
            // A token we cannot persist still works for the rest of this session.
        }
    }

    /// <summary>Clears both tokens - the in-memory access token and the persisted refresh
    /// token. Used for an explicit sign-out and for a refresh that turned out to be unusable
    /// (see HemordnaApiClient.RefreshAccessTokenAsync and its SignedOutUnexpectedly event).</summary>
    public async Task ClearAsync()
    {
        AccessToken = null;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenStorageKey);
        }
        catch (JSException)
        {
        }
    }

    /// <summary>
    /// One-time cleanup for everyone upgrading from before the access token moved to memory:
    /// their browser still has a real, usable access token sitting under the old
    /// <c>"hemordna.token"</c> key, written by the previous client and never read by this one
    /// again. Left alone, it would keep working as a bearer credential - unused by the app, but
    /// not actually revoked - until it naturally expired, which is exactly the kind of leftover a
    /// security change should not leave behind. Called once at startup (see Program.cs) rather
    /// than folded into GetRefreshTokenAsync/SetRefreshTokenAsync above, since it has nothing to
    /// do with the refresh token itself - only with retiring the old key. Safe to call even when
    /// the key was never set.
    /// </summary>
    public async Task RemoveLegacyAccessTokenAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", LegacyAccessTokenStorageKey);
        }
        catch (JSException)
        {
        }
    }
}
