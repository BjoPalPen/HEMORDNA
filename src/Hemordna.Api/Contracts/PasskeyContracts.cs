namespace Hemordna.Api.Contracts;

public sealed record PasskeyLoginOptionsRequest(string? Email);

/// <summary><see cref="Id"/> is the credential id, base64url-encoded - the same form the
/// browser and Fido2NetLib already use, so it round-trips as a route value with no extra
/// encoding.</summary>
public sealed record PasskeyResponse(string Id, string DeviceLabel, DateTimeOffset CreatedAt);
