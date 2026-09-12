namespace Hemordna.Api.Authentication;

/// <summary>
/// Restricts a per-member route to the caller's own member id, or to any member when the caller
/// can manage the household.
/// </summary>
/// <remarks>
/// Pausing a member sits between the two rules the other per-member routes use. It is not as
/// private as a presentation mode or a day off (<see cref="MemberSelfAccessFilter"/>, which never
/// lets one account holder reach into another's), because pausing someone changes what the
/// household's own schedule generates for them. But it is not household configuration either -
/// "jag reser bort" is something every member says about themselves.
/// <para>
/// Hence: your own, always; anyone else's only with
/// <see cref="Hemordna.Application.Households.HouseholdMembership.CanManageHousehold"/>. An
/// account-less member is covered by that second branch rather than by an exception of its own -
/// someone who manages the household can pause the child who cannot sign in.
/// </para>
/// <para>
/// Returns 403 for the same reason the two sibling filters do - see
/// <see cref="MemberSelfAccessFilter"/>'s own remarks.
/// </para>
/// </remarks>
internal sealed class MemberSelfOrManageFilter : IEndpointFilter
{
    private const string RouteParameterName = "memberId";

    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var membership = httpContext.GetMembership();

        if (httpContext.Request.RouteValues[RouteParameterName] is not string routeValue
            || !Guid.TryParse(routeValue, out var memberId))
        {
            throw new InvalidOperationException(
                $"MemberSelfOrManageFilter requires a '{RouteParameterName}' route parameter.");
        }

        if (memberId == membership.MemberId || membership.CanManageHousehold)
        {
            return next(context);
        }

        return ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status403Forbidden));
    }
}
