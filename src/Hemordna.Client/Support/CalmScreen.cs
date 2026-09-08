using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Lugnare skärm" (Inställningar) - a per-device choice, exactly like <see cref="Theme"/>, not
/// a member preference: it is a property of the screen someone is holding, not of the person
/// (see docs/PRODUCT.md §7, docs/ARCHITECTURE.md "Beslut: Ångra och stabil lista", Del C). Turns
/// off transitions, animations and translucent/blur effects app-wide via <c>data-calm</c> on
/// <c>&lt;html&gt;</c> - see app.css.
/// </summary>
internal static class CalmScreen
{
    public static async Task<bool> GetAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/calm-screen.js");
        return await module.InvokeAsync<bool>("get");
    }

    public static async Task SetAsync(IJSRuntime js, bool enabled)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/calm-screen.js");
        await module.InvokeVoidAsync("set", enabled);
    }
}
