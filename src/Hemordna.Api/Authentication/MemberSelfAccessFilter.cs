using Hemordna.Application.Households;

namespace Hemordna.Api.Authentication;

/// <summary>
/// Restricts a personal, per-member route to the caller's own member id - or to a member in the
/// same household who has no account of their own.
/// </summary>
/// <remarks>
/// A member with no account (<see cref="Hemordna.Domain.Households.HouseholdMember.UserId"/> is
/// <c>null</c> - a child, a partner who has not signed up yet) can never sign in to set this for
/// themselves, so someone else in the household has to be able to. That is the only exception:
/// a caller can never reach into another account-holding member's own personal settings.
/// <para>
/// Runs inside the <c>scoped</c> group, after <see cref="HouseholdAccessFilter"/> has already
/// resolved and verified the caller's own membership - see <see cref="CallerExtensions.GetMembership"/>.
/// </para>
/// <para>
/// Returns 403, not 404, on a mismatch. This is the opposite of
/// <see cref="HouseholdAccessFilter"/>'s own choice, and deliberately so: there, the caller is a
/// stranger to the household and a 404 hides whether it even exists. Here, the caller is already
/// a verified member of this very household - they already know the target member exists (they
/// see them in the household's own member list). A 404 would be a lie that keeps the client from
/// telling "does not exist" apart from "not allowed", for no privacy gained.
/// </para>
/// </remarks>
internal sealed class MemberSelfAccessFilter : IEndpointFilter
{
    private const string RouteParameterName = "memberId";

    private readonly IHouseholdRepository _households;

    public MemberSelfAccessFilter(IHouseholdRepository households) => _households = households;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var membership = httpContext.GetMembership();

        if (httpContext.Request.RouteValues[RouteParameterName] is not string routeValue
            || !Guid.TryParse(routeValue, out var memberId))
        {
            throw new InvalidOperationException(
                $"MemberSelfAccessFilter requires a '{RouteParameterName}' route parameter.");
        }

        if (memberId == membership.MemberId)
        {
            return await next(context);
        }

        var household = await _households.FindByIdAsync(membership.HouseholdId, httpContext.RequestAborted);
        var target = household?.Members.FirstOrDefault(member => member.Id == memberId);

        // An unknown member is not this filter's call to make: let the handler's own lookup
        // answer with its usual 404, same as any other route parameter naming something that
        // does not exist.
        if (target is null || target.UserId is null)
        {
            return await next(context);
        }

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
}
