using Microsoft.JSInterop;

namespace Hemordna.Client.Support;

/// <summary>
/// "Jag börjar nu" (MinDag.razor) - a per-device, per-day marker for the single task someone is
/// currently working on, exactly like <see cref="CalmScreen"/> and <see cref="Theme"/>: a
/// property of the device/moment, never a <c>MemberPreference</c> and never visible to another
/// household member (docs/ARCHITECTURE.md, "Beslut: Ångra och stabil lista", Del C). Storing the
/// date alongside the occurrence, rather than just the occurrence, is what makes a stale marker
/// from a previous day self-expire: <see cref="GetAsync"/> ignores an entry whose date is not
/// <c>today</c> instead of requiring a separate cleanup step. Only one occurrence is ever stored
/// at a time, so starting a new task naturally replaces whatever was started before.
/// </summary>
internal static class StartedTask
{
    private sealed record StoredStartedTask(string Date, Guid OccurrenceId);

    public static async Task<Guid?> GetAsync(IJSRuntime js, DateOnly today)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/started-task.js");
        var stored = await module.InvokeAsync<StoredStartedTask?>("get");
        return stored is not null && stored.Date == Key(today) ? stored.OccurrenceId : null;
    }

    public static async Task SetAsync(IJSRuntime js, DateOnly today, Guid occurrenceId)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/started-task.js");
        await module.InvokeVoidAsync("set", Key(today), occurrenceId);
    }

    public static async Task ClearAsync(IJSRuntime js)
    {
        await using var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/started-task.js");
        await module.InvokeVoidAsync("clear");
    }

    private static string Key(DateOnly date) => date.ToString("yyyy-MM-dd");
}
