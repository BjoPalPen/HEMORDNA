using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Utseende" (Inställningar) - ljust/mörkt/systemets eget. A per-device preference (like text
/// size used to be before it moved server-side): stored in localStorage, not
/// <c>MemberPreference</c>, since the right theme depends on this screen's own environment, not
/// on who is signed in. <c>wwwroot/index.html</c> runs the same logic inline, synchronously,
/// before the stylesheet loads, so the correct theme is already applied on first paint - this
/// class is what Inställningar itself uses to read and change the choice afterwards.
/// </summary>
internal static class Theme
{
    public static async Task<string> GetAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
        return await module.InvokeAsync<string>("get");
    }

    public static async Task SetAsync(IJSRuntime js, string choice)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
        await module.InvokeVoidAsync("set", choice);
    }
}
