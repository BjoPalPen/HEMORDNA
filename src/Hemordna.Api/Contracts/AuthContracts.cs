namespace Hemordna.Api.Contracts;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record AccessTokenResponse(string Token, DateTimeOffset ExpiresAt);

/// <summary>The caller's own identity and household membership, if they have one yet.</summary>
public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? HouseholdId,
    Guid? MemberId);
