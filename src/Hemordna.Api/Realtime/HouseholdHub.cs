using Hemordna.Api.Authentication;
using Hemordna.Application.Households;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hemordna.Api.Realtime;

/// <summary>
/// One hub, one group per household - see docs/ARCHITECTURE.md §5. A client joins after
/// signing in and stays isolated to its own household's events, the same boundary the REST
/// API enforces.
/// </summary>
[Authorize]
public sealed class HouseholdHub : Hub
{
    private readonly IHouseholdMembershipQuery _memberships;

    public HouseholdHub(IHouseholdMembershipQuery memberships) => _memberships = memberships;

    internal static string GroupName(Guid householdId) => $"household:{householdId}";

    /// <summary>
    /// Joins the caller's own household group. Membership is checked here too, not assumed
    /// from the connection being authenticated - the same rule HouseholdAccessFilter applies
    /// to REST calls.
    /// </summary>
    public async Task JoinHousehold(Guid householdId)
    {
        var userId = Context.User?.GetUserId();

        if (userId is null)
        {
            throw new HubException("Not signed in.");
        }

        var membership = await _memberships.FindByUserIdAsync(userId.Value, Context.ConnectionAborted);

        if (membership is null || membership.HouseholdId != householdId)
        {
            // Same stance as the REST boundary: do not confirm whether the household exists.
            throw new HubException("Not a member of this household.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(householdId));
    }
}
