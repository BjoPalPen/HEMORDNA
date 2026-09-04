using Microsoft.JSInterop;

namespace Hemordna.Client.Services;

/// <summary>
/// Keeps the access token in the browser's local storage so a reload does not sign the user
/// out. Every read is defensive: storage can be unavailable or cleared.
/// </summary>
public sealed class TokenStore
{
    private const string StorageKey = "hemordna.token";

    private readonly IJSRuntime _js;

    public TokenStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task SetAsync(string token)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, token);
        }
        catch (JSException)
        {
            // A token we cannot persist still works for this session.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
        }
    }
}
