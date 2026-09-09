using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Läs upp" (fokusläget, MinDag.razor) - a thin wrapper over the Web Speech API
/// (<c>js/speech.js</c>). No per-device or per-member state to persist (unlike
/// <see cref="Theme"/>/<see cref="CalmScreen"/>/<see cref="StartedTask"/>): every press starts a
/// fresh utterance, and there is nothing to remember between visits.
/// </summary>
internal static class Speech
{
    public static async Task<bool> IsAvailableAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
        return await module.InvokeAsync<bool>("isAvailable");
    }

    public static async Task<bool> HasSwedishVoiceAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
        return await module.InvokeAsync<bool>("hasSwedishVoice");
    }

    /// <summary>Cancels whatever was already playing, same as the button's own "one at a time"
    /// behaviour. <paramref name="selfRef"/> receives the JS side's "OnSpeechEnded" callback once
    /// the utterance actually finishes (or errors) - a caller-owned reference, not created here,
    /// since it needs to outlive this one call.</summary>
    public static async Task SpeakAsync<T>(IJSRuntime js, DotNetObjectReference<T> selfRef, string text)
        where T : class
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
        await module.InvokeVoidAsync("speak", selfRef, text);
    }

    public static async Task StopAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
        await module.InvokeVoidAsync("stop");
    }
}
