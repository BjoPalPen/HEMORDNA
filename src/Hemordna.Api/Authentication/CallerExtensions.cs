using System.Security.Claims;
using Hemordna.Application.Households;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Hemordna.Api.Authentication;

/// <summary>Reads the caller's identity and resolved household membership off the request.</summary>
internal static class CallerExtensions
{
    private const string MembershipItemKey = "Hemordna.HouseholdMembership";

    /// <summary>The authenticated user's id, or <c>null</c> when the token carries no usable subject.</summary>
    internal static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    internal static void SetMembership(this HttpContext context, HouseholdMembership membership)
        => context.Items[MembershipItemKey] = membership;

    /// <summary>
    /// The membership resolved by <see cref="HouseholdAccessFilter"/>. Only call this from a
    /// handler behind that filter - it is what guarantees the value is present and checked.
    /// </summary>
    internal static HouseholdMembership GetMembership(this HttpContext context)
        => context.Items[MembershipItemKey] as HouseholdMembership
            ?? throw new InvalidOperationException(
                "No household membership on the request. The endpoint is missing HouseholdAccessFilter.");
}
