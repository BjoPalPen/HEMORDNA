using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Hur är orken idag?" (Sju enkla lösningar, del 1) - which of the three <see cref="EnergyLevel"/>
/// labels was chosen today, remembered per-device (<c>hemordna.energy</c> in
/// <c>localStorage</c>, mirrors <see cref="StartedTask"/> exactly) purely so the chip shows the
/// right state after a reload. The server never stores or sees the level itself - only the
/// resulting minutes (<c>SetAvailabilityAsync</c>) ever reach it.
/// </summary>
internal static class EnergyChoice
{
    private sealed record StoredEnergyChoice(string Date, string Level);

    public static async Task<string?> GetAsync(IJSRuntime js, DateOnly today)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/energy-choice.js");
        var stored = await module.InvokeAsync<StoredEnergyChoice?>("get");
        return stored is not null && stored.Date == Key(today) ? stored.Level : null;
    }

    public static async Task SetAsync(IJSRuntime js, DateOnly today, string level)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/energy-choice.js");
        await module.InvokeVoidAsync("set", Key(today), level);
    }

    private static string Key(DateOnly date) => date.ToString("yyyy-MM-dd");
}
