using Hemordna.Application.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Hemordna.Api.Realtime;

/// <summary>
/// Implements Application's realtime boundary with SignalR. Kept to a single coarse client
/// method - see <see cref="IHouseholdNotifier"/> for why.
/// </summary>
internal sealed class SignalRHouseholdNotifier : IHouseholdNotifier
{
    private const string ClientMethod = "OccurrencesChanged";

    private readonly IHubContext<HouseholdHub> _hub;

    public SignalRHouseholdNotifier(IHubContext<HouseholdHub> hub) => _hub = hub;

    public Task NotifyOccurrencesChangedAsync(Guid householdId, CancellationToken cancellationToken)
        => _hub.Clients
            .Group(HouseholdHub.GroupName(householdId))
            .SendAsync(ClientMethod, cancellationToken: cancellationToken);
}
