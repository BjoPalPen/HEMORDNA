using Hemordna.Domain.Common;

namespace Hemordna.Domain.Push;

/// <summary>
/// One browser's Web Push subscription for one member's own device - the Push API's endpoint
/// plus the two keys needed to encrypt a notification for it (RFC 8291). Deliberately no notion
/// of what gets sent or when: there is no scheduler and no policy here, only "this device wants
/// to be reachable" - see CLAUDE.md's scope note for this task. Sending, VAPID and the 410/404
/// cleanup all live in Infrastructure behind <c>IPushSender</c>.
/// </summary>
public sealed class PushSubscription
{
    /// <summary>Generous headroom over observed push service endpoint URLs (typically well under 300 characters).</summary>
    public const int MaxEndpointLength = 1000;

    /// <summary>The P256DH key is a base64url-encoded 65-byte uncompressed EC point (~87 characters).</summary>
    public const int MaxP256dhLength = 128;

    /// <summary>The auth secret is a base64url-encoded 16-byte value (~22 characters).</summary>
    public const int MaxAuthLength = 64;

    private PushSubscription(
        Guid id,
        Guid householdId,
        Guid memberId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset createdAt)
    {
        Id = id;
        HouseholdId = householdId;
        MemberId = memberId;
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        CreatedAt = createdAt;
        LastSeenAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>The member whose device this is.</summary>
    public Guid MemberId { get; private set; }

    /// <summary>The push service URL this device subscribed with. Unique across the whole
    /// table - a given browser installation has exactly one live subscription, regardless of
    /// which member most recently subscribed from it; see <see cref="Reclaim"/>.</summary>
    public string Endpoint { get; private set; }

    /// <summary>The subscription's P256DH public key, base64url-encoded.</summary>
    public string P256dh { get; private set; }

    /// <summary>The subscription's auth secret, base64url-encoded.</summary>
    public string Auth { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When this device last (re-)subscribed. Not touched by a send - only by
    /// <see cref="Reclaim"/> - so it answers "does the browser still want this", not
    /// "did we last succeed sending to it".</summary>
    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Registers a new device subscription.</summary>
    public static PushSubscription Subscribe(
        Guid householdId,
        Guid memberId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset createdAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));

        return new PushSubscription(
            Guid.NewGuid(),
            householdId,
            memberId,
            ValidateEndpoint(endpoint),
            ValidateKey(p256dh, MaxP256dhLength, nameof(p256dh)),
            ValidateKey(auth, MaxAuthLength, nameof(auth)),
            createdAt);
    }

    /// <summary>
    /// Re-subscribing an existing endpoint - the same browser install subscribing again (its
    /// keys can rotate) or a different member of the household picking up a shared device.
    /// The unique index on <see cref="Endpoint"/> is what makes this the only way a second
    /// subscription for the same endpoint is ever recorded, so the caller reassigns ownership
    /// here rather than creating a duplicate row.
    /// </summary>
    public void Reclaim(Guid householdId, Guid memberId, string p256dh, string auth, DateTimeOffset seenAt)
    {
        HouseholdId = Guard.AgainstEmpty(householdId, nameof(householdId));
        MemberId = Guard.AgainstEmpty(memberId, nameof(memberId));
        P256dh = ValidateKey(p256dh, MaxP256dhLength, nameof(p256dh));
        Auth = ValidateKey(auth, MaxAuthLength, nameof(auth));
        LastSeenAt = seenAt;
    }

    private static string ValidateEndpoint(string endpoint)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(endpoint, nameof(endpoint));

        if (trimmed.Length > MaxEndpointLength)
        {
            throw new ArgumentException(
                $"Endpoint must be at most {MaxEndpointLength} characters.", nameof(endpoint));
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Endpoint must be an absolute https URL.", nameof(endpoint));
        }

        return trimmed;
    }

    private static string ValidateKey(string value, int maxLength, string parameterName)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(value, parameterName);

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException(
                $"Value must be at most {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
