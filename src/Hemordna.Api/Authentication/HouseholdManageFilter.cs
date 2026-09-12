namespace Hemordna.Api.Authentication;

/// <summary>
/// Restricts a household-configuration route (rooms, tasks, members, invite code, pause,
/// reset - see docs/ARCHITECTURE.md "Beslut: Vem får ändra vad") to a caller whose membership
/// carries <see cref="Hemordna.Application.Households.HouseholdMembership.CanManageHousehold"/>.
/// </summary>
/// <remarks>
/// Runs inside the <c>scoped</c> group, after <see cref="HouseholdAccessFilter"/> has already
/// resolved and verified the caller's own membership - this filter only reads the flag it
/// already carries, no extra lookup needed.
/// <para>
/// Returns 403, not 404, for the same reason <see cref="MemberSelfAccessFilter"/> does: the
/// caller is already a verified member of this household, not a stranger to it - see that
/// filter's own remarks.
/// </para>
/// </remarks>
internal sealed class HouseholdManageFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var membership = context.HttpContext.GetMembership();

        if (!membership.CanManageHousehold)
        {
            return ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status403Forbidden));
        }

        return next(context);
    }
}
