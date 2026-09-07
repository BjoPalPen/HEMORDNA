using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// Applies the "Stor text" presentation mode (docs/DESIGN.md §7) by toggling
/// <c>data-text-size="large"</c> on <c>&lt;html&gt;</c> - app.css scales <c>--font-size-base</c>
/// from there, so this is the only place that needs to know the attribute's name.
/// </summary>
internal static class TextSize
{
    public static async Task ApplyAsync(IJSRuntime js, bool large)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/text-size.js");
        await module.InvokeVoidAsync("apply", large);
    }
}
