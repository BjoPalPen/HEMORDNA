namespace Hemordna.Api.Contracts;

/// <summary>The public VAPID key a client needs before it can create a push subscription.</summary>
public sealed record VapidPublicKeyResponse(string PublicKey);

public sealed record SubscribeToPushRequest(string? Endpoint, string? P256dh, string? Auth);

public sealed record UnsubscribeFromPushRequest(string? Endpoint);

/// <summary>How many of the caller's own devices actually received the test notification.</summary>
public sealed record SendTestPushResponse(int Sent);
