using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// Toggles <c>data-scrolled</c> on <c>&lt;html&gt;</c> as the page scrolls (app.css/NavMenu.razor.css/
/// Pages/MinDag.razor.css read it from there) - a single, app-wide listener attached once from
/// <c>MainLayout.OnAfterRenderAsync(firstRender)</c> rather than one per page.
/// </summary>
internal static class ScrollState
{
    private static IJSObjectReference? _module;

    public static async Task AttachAsync(IJSRuntime js)
    {
        _module = await js.InvokeAsync<IJSObjectReference>("import", "./js/scroll-state.js");
        await _module.InvokeVoidAsync("attach");
    }

    /// <summary>
    /// Clears a stale <c>data-scrolled</c> left over from wherever the member was before a
    /// route change - see <c>MainLayout.razor</c>'s own <c>LocationChanged</c> subscription and
    /// docs/ARCHITECTURE.md "Uppdrag: fokuskortet". A no-op before <see cref="AttachAsync"/> has
    /// run (an early redirect, e.g. <c>Redirect.razor</c>, can navigate before
    /// <c>MainLayout.OnAfterRenderAsync(firstRender)</c> gets a chance to attach the module).
    /// </summary>
    public static async Task ResetAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("reset");
        }
    }
}
