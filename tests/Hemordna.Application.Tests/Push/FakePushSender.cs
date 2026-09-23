using Hemordna.Application.Push;
using Hemordna.Domain.Push;

namespace Hemordna.Application.Tests.Push;

internal sealed class FakePushSender : IPushSender
{
    internal int SentCount { get; set; }

    internal IReadOnlyList<PushSubscription>? LastSubscriptions { get; private set; }

    internal string? LastTitle { get; private set; }

    internal string? LastBody { get; private set; }

    internal string? LastUrl { get; private set; }

    public string GetVapidPublicKey() => "fake-public-key";

    public Task<int> SendAsync(
        IReadOnlyList<PushSubscription> subscriptions,
        string title,
        string body,
        string? url,
        CancellationToken cancellationToken)
    {
        LastSubscriptions = subscriptions;
        LastTitle = title;
        LastBody = body;
        LastUrl = url;
        return Task.FromResult(SentCount);
    }
}
