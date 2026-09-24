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
    /// <summary>
    /// "Kan den här enheten tala?" - never throws because the module could not be loaded (an old
    /// browser, an interrupted download). MinDag.razor's OnInitializedAsync calls this
    /// unprotected, and a failed import must not block the whole page the way it once did - see
    /// HouseholdRealtimeClient.ConnectAsync's own remarks for the same class of bug. <c>false</c>
    /// here means "could not confirm it works", not "this device is proven unable to speak" - the
    /// natural answer to the question either way, but worth reading correctly: a later visit could
    /// still succeed. An <see cref="OperationCanceledException"/> from a shutdown in progress is
    /// let through unchanged, since that is not a module-loading failure.
    /// </summary>
    public static async Task<bool> IsAvailableAsync(IJSRuntime js)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
            return await module.InvokeAsync<bool>("isAvailable");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Same non-throwing contract as <see cref="IsAvailableAsync"/>, for the same reason
    /// - a failed module import answers "could not confirm a Swedish voice exists" as
    /// <c>false</c>, not as a thrown exception.</summary>
    public static async Task<bool> HasSwedishVoiceAsync(IJSRuntime js)
    {
        try
        {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/speech.js");
            return await module.InvokeAsync<bool>("hasSwedishVoice");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
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
