using Hemordna.Application.Households;

namespace Hemordna.Api.Authentication;

/// <summary>
/// Enforces the tenant boundary: the caller must be an active member of the household named
/// in the route.
/// </summary>
/// <remarks>
/// This runs before every household-scoped handler, so a handler cannot forget the check.
/// Membership is looked up per request rather than trusted from a token claim, so revoking
/// someone's membership takes effect immediately instead of when their token expires.
/// <para>
/// A caller asking for a household they do not belong to gets 404, not 403: telling them the
/// household exists would leak that fact to someone with no right to know it.
/// </para>
/// </remarks>
internal sealed class HouseholdAccessFilter : IEndpointFilter
{
    private const string RouteParameterName = "householdId";

    private readonly IHouseholdMembershipQuery _memberships;

    public HouseholdAccessFilter(IHouseholdMembershipQuery memberships) => _memberships = memberships;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (httpContext.Request.RouteValues[RouteParameterName] is not string routeValue
            || !Guid.TryParse(routeValue, out var householdId))
        {
            throw new InvalidOperationException(
                $"HouseholdAccessFilter requires a '{RouteParameterName}' route parameter.");
        }

        var membership = await _memberships.FindByUserIdAsync(userId, httpContext.RequestAborted);

        if (membership is null || membership.HouseholdId != householdId)
        {
            return Results.NotFound();
        }

        httpContext.SetMembership(membership);

        return await next(context);
    }
}
