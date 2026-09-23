using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Push;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// Transport for a member's own push subscriptions and for triggering a test notification by
/// hand. No scheduler, no notification types, no policy - see CLAUDE.md's scope note for this
/// task; the endpoints map and delegate, every rule lives in the domain and the use cases.
/// </summary>
/// <remarks>
/// Same shape as <see cref="ReminderEndpoints"/>: behind <see cref="HouseholdAccessFilter"/>,
/// not <c>manage</c> - a device subscription is not household configuration - and the owner is
/// always <c>httpContext.GetMembership().MemberId</c>, never taken from the request body.
/// </remarks>
internal static class PushEndpoints
{
    internal static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var push = app.MapGroup("/api/households/{householdId:guid}/push")
            .WithTags("Push")
            .RequireAuthorization()
            .AddEndpointFilter<HouseholdAccessFilter>();

        push.MapGet("/vapid-public-key", GetVapidPublicKeyAsync)
            .Produces<VapidPublicKeyResponse>();

        push.MapPost("/subscribe", SubscribeAsync)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        push.MapPost("/unsubscribe", UnsubscribeAsync)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        push.MapPost("/test", SendTestAsync)
            .Produces<SendTestPushResponse>();

        return app;
    }

    private static IResult GetVapidPublicKeyAsync(IPushSender pushSender)
        => Results.Ok(new VapidPublicKeyResponse(pushSender.GetVapidPublicKey()));

    private static async Task<IResult> SubscribeAsync(
        Guid householdId,
        HttpContext httpContext,
        SubscribeToPushRequest request,
        SubscribeToPush subscribeToPush,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Subscription"] = ["Endpoint, p256dh och auth måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        await subscribeToPush.HandleAsync(
            householdId, membership.MemberId, request.Endpoint, request.P256dh, request.Auth, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> UnsubscribeAsync(
        Guid householdId,
        HttpContext httpContext,
        UnsubscribeFromPushRequest request,
        UnsubscribeFromPush unsubscribeFromPush,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Endpoint)] = ["Endpoint måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        await unsubscribeFromPush.HandleAsync(householdId, membership.MemberId, request.Endpoint, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> SendTestAsync(
        Guid householdId,
        HttpContext httpContext,
        SendTestPushNotification sendTestPushNotification,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var sent = await sendTestPushNotification.HandleAsync(householdId, membership.MemberId, cancellationToken);

        return Results.Ok(new SendTestPushResponse(sent));
    }
}
