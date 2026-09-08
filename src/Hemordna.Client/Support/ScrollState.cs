using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// Toggles <c>data-scrolled</c> on <c>&lt;html&gt;</c> as the page scrolls (app.css/NavMenu.razor.css/
/// Pages/MinDag.razor.css read it from there) - a single, app-wide listener attached once from
/// <c>MainLayout.OnAfterRenderAsync(firstRender)</c> rather than one per page.
/// </summary>
internal static class ScrollState
{
    public static async Task AttachAsync(IJSRuntime js)
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/scroll-state.js");
        await module.InvokeVoidAsync("attach");
    }
}
