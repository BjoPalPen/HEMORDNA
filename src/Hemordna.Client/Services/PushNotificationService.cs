using Microsoft.JSInterop;

namespace Hemordna.Client.Services;

/// <summary>Thin wrapper over wwwroot/js/push.js - mirrors <see cref="WebAuthnClient"/>'s shape.
/// Owns nothing of the HTTP contract; <see cref="HemordnaApiClient"/> still does that.</summary>
public sealed class PushNotificationService
{
    private readonly IJSRuntime _js;

    public PushNotificationService(IJSRuntime js) => _js = js;

    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("hemordnaPush.isSupported");
        }
        catch (Exception)
        {
            // A capability probe must never take its caller down with it - see
            // WebAuthnClient.IsAvailableAsync's own remarks.
            return false;
        }
    }

    /// <summary>True when running in an ordinary iOS Safari tab, not added to the home screen -
    /// the one case where push silently cannot work at all. See docs/PRODUCT.md §7/§8 for why
    /// Installningar.razor's notice about it stays calm and says nothing about who is asking.</summary>
    public async Task<bool> IsIosSafariTabAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("hemordnaPush.isIosSafariTab");
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>"granted", "denied", "default", or "unsupported".</summary>
    public async Task<string> GetPermissionAsync()
    {
        try
        {
            return await _js.InvokeAsync<string>("hemordnaPush.getPermission");
        }
        catch (Exception)
        {
            return "unsupported";
        }
    }

    public async Task<bool> IsSubscribedAsync()
    {
        try
        {
            var subscription = await _js.InvokeAsync<SubscriptionInfo>("hemordnaPush.getSubscription");
            return subscription.Endpoint is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Requests notification permission and subscribes. Must be called directly from a button's
    /// click handler with no <c>await</c> beforehand in the caller - iOS only honours the
    /// permission prompt as a direct result of a real press (see push.js's own remarks and
    /// CLAUDE.md's task instructions). The browser hands back the subscription's keys exactly
    /// once, here - there is no separate call to re-read them later, so the caller sends them to
    /// <c>HemordnaApiClient.SubscribeToPushAsync</c> straight from this result.
    /// </summary>
    public async Task<PushSubscribeResult> SubscribeAsync(string vapidPublicKey)
    {
        var result = await _js.InvokeAsync<SubscribeResult>("hemordnaPush.subscribe", vapidPublicKey);

        return result.Success && result.Endpoint is not null && result.P256dh is not null && result.Auth is not null
            ? PushSubscribeResult.ForSuccess(result.Endpoint, result.P256dh, result.Auth)
            : PushSubscribeResult.ForFailure(result.Error ?? "Det gick inte att aktivera notiser.");
    }

    /// <summary>Unsubscribes this device. Returns the endpoint that was unsubscribed (needed to
    /// tell the server which row to remove), or <c>null</c> on failure - there was nothing to
    /// unsubscribe locally either way, so the caller has nothing to send.</summary>
    public async Task<PushUnsubscribeResult> UnsubscribeAsync()
    {
        var result = await _js.InvokeAsync<UnsubscribeResult>("hemordnaPush.unsubscribe");

        return result.Success
            ? PushUnsubscribeResult.ForSuccess(result.Endpoint)
            : PushUnsubscribeResult.ForFailure(result.Error ?? "Det gick inte att stänga av notiser.");
    }

    private sealed class SubscriptionInfo
    {
        public string? Endpoint { get; set; }
    }

    private sealed class SubscribeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Endpoint { get; set; }
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }

    private sealed class UnsubscribeResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Endpoint { get; set; }
    }
}

/// <summary>Either a new subscription's fields, ready for the server call, or why subscribing
/// failed - see <see cref="PushNotificationService.SubscribeAsync"/>.</summary>
public sealed record PushSubscribeResult(bool Success, string? Error, string? Endpoint, string? P256dh, string? Auth)
{
    public static PushSubscribeResult ForSuccess(string endpoint, string p256dh, string auth)
        => new(true, null, endpoint, p256dh, auth);

    public static PushSubscribeResult ForFailure(string error) => new(false, error, null, null, null);
}

/// <summary>See <see cref="PushNotificationService.UnsubscribeAsync"/>.</summary>
public sealed record PushUnsubscribeResult(bool Success, string? Error, string? Endpoint)
{
    /// <summary><paramref name="endpoint"/> is <c>null</c> when there was no subscription to
    /// begin with - still a success, just nothing for the caller to tell the server about.</summary>
    public static PushUnsubscribeResult ForSuccess(string? endpoint) => new(true, null, endpoint);

    public static PushUnsubscribeResult ForFailure(string error) => new(false, error, null);
}
