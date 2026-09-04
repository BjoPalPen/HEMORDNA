using Microsoft.AspNetCore.Identity;

namespace Hemordna.Infrastructure.Identity;

/// <summary>
/// The sign-in identity. Deliberately thin: it holds credentials and nothing about the
/// household. Who someone is in a household is <see cref="Domain.Households.HouseholdMember"/>,
/// linked by <c>UserId</c>, so identity and domain can evolve independently.
/// </summary>
public sealed class HemordnaUser : IdentityUser<Guid>
{
    /// <summary>The name used when creating the person's first household member.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
