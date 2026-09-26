namespace Hemordna.Api.Contracts;

public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record ForgotPasswordRequest(string? Email);

public sealed record ResetPasswordRequest(string? Email, string? Token, string? NewPassword);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

/// <summary>
/// <see cref="Token"/> and <see cref="ExpiresAt"/> are the access token - the original two
/// fields this response has always had, kept under their original names on purpose: the current
/// client (not yet updated to read a refresh token - that lands in a later commit) already
/// deserializes exactly this shape for register, login, passkey login and change-password, and
/// renaming them would silently break it (a missing property in the JSON does not throw, it just
/// leaves the client's copy holding <c>null</c>). <see cref="RefreshToken"/> and
/// <see cref="RefreshTokenExpiresAt"/> are new, additive, and <c>null</c> wherever no refresh
/// token is issued or renewed alongside the access token (currently only change-password).
/// </summary>
public sealed record AccessTokenResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    string? RefreshToken = null,
    DateTimeOffset? RefreshTokenExpiresAt = null);

/// <summary>The refresh token body for both <c>POST /api/auth/refresh</c> and
/// <c>POST /api/auth/logout</c> - both act on whichever chain this token belongs to.</summary>
public sealed record RefreshTokenRequest(string? RefreshToken);

/// <summary>The caller's own identity and household membership, if they have one yet.</summary>
public sealed record MeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? HouseholdId,
    Guid? MemberId,
    bool CanManageHousehold);
