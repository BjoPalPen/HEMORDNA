namespace Hemordna.Application.Realtime;

/// <summary>
/// Tells a household's other connected clients that something changed, so PRODUCT.md §6's
/// requirement - "andra anslutna klienter ska se det utan manuell omladdning" - holds without
/// Application knowing anything about SignalR, WebSockets or HTTP. Implemented in Api, where
/// the hub lives.
/// </summary>
public interface IHouseholdNotifier
{
    /// <summary>
    /// A task occurrence in this household was created, assigned, completed, deferred or
    /// skipped. Deliberately a single coarse event rather than one per kind of change: every
    /// connected client just re-fetches its own day, which is simple, always correct, and
    /// avoids designing a per-event payload before there is a second consumer that needs one.
    /// </summary>
    Task NotifyOccurrencesChangedAsync(Guid householdId, CancellationToken cancellationToken);
}
