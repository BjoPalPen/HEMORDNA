using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Utseende" (Inställningar) - a per-device backdrop photo, same category of preference as
/// <see cref="Theme"/> and <see cref="CalmScreen"/>: it belongs to this screen, never to
/// <c>MemberPreference</c> or any other household member, and it never leaves the device.
/// <c>wwwroot/js/backdrop.js</c> stores the image as a Blob in IndexedDB (a photo does not fit
/// localStorage's ~5 MB quota) and applies it as a CSS custom property + attribute on
/// <c>&lt;html&gt;</c>; <c>wwwroot/index.html</c> re-applies it as early as possible on boot.
/// This class is what Inställningar itself uses afterwards to choose, check or remove the image.
/// </summary>
internal static class Backdrop
{
    /// <summary>
    /// Downscales and stores the first file of the given <c>&lt;input type="file"&gt;</c>
    /// element, then applies it immediately. Throws a <see cref="JSException"/> with a message
    /// safe to show to the person (e.g. "Bilden gick inte att sparas på den här enheten.") if
    /// storage is full or blocked.
    /// </summary>
    public static async Task SetFromInputAsync(IJSRuntime js, ElementReference input)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/backdrop.js");
        await module.InvokeVoidAsync("set", input);
    }

    /// <summary>Whether a backdrop image is currently stored - never the image itself.</summary>
    public static async Task<bool> GetAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/backdrop.js");
        return await module.InvokeAsync<bool>("get");
    }

    /// <summary>Removes the stored image, if any, and un-applies it.</summary>
    public static async Task ClearAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/backdrop.js");
        await module.InvokeVoidAsync("clear");
    }

    /// <summary>
    /// Opens the native file picker for the given hidden <c>&lt;input type="file"&gt;</c> -
    /// Blazor has no built-in way to click an element from C#, so Inställningar's own styled
    /// "Välj bild" button calls this instead of the real, hidden input.
    /// </summary>
    public static async Task ClickFileInputAsync(IJSRuntime js, ElementReference input)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/backdrop.js");
        await module.InvokeVoidAsync("click", input);
    }
}
