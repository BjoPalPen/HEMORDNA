namespace Hemordna.Infrastructure.Identity;

/// <summary>
/// A WebAuthn credential (Face ID, Touch ID, Windows Hello, a fingerprint sensor) registered
/// as an alternative to signing in with a password. Only the public key and a counter are
/// stored - the private key never leaves the person's device, so this table on its own is
/// useless for signing in as anyone.
/// </summary>
public sealed class PasskeyCredential
{
    public required byte[] CredentialId { get; init; }

    public required Guid UserId { get; init; }

    public required byte[] PublicKey { get; init; }

    /// <summary>
    /// The authenticator's own use counter, so a cloned credential (extracted from one device
    /// and replayed from another) can be detected: a valid assertion's counter must always be
    /// higher than the last one seen - see AuthEndpoints.VerifyPasskeyLoginAsync.
    /// </summary>
    public required uint SignCount { get; set; }

    /// <summary>A human label ("iPhone", "Windows-dator") so a person can tell their passkeys
    /// apart on the settings page - guessed from the User-Agent at registration time.</summary>
    public required string DeviceLabel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
