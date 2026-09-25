using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// Transport for reminders - a member's own (see docs/PRODUCT.md §11) and, since the
/// reminder-visibility feature, what other household members are allowed to see of everyone
/// else's. The endpoints map and delegate; every rule (including the privacy boundary) lives in
/// the domain and the use cases.
/// </summary>
/// <remarks>
/// All routes run behind <see cref="HouseholdAccessFilter"/>, same as
/// <see cref="HouseholdEndpoints"/>'s <c>scoped</c> group. Deliberately not on <c>manage</c> - a
/// reminder is not household configuration, every member handles their own - and not on
/// <c>selfOnly</c> either, since there is no <c>memberId</c> route parameter to check: the owner
/// is always <c>httpContext.GetMembership().MemberId</c>, the same pattern
/// <c>GetTimeCreditAsync</c> and <c>CreateExtraTaskAsync</c> already use for "always the caller's
/// own". A caller can therefore never name another member's reminder by id, and every
/// single-reminder use case already treats "belongs to someone else" identically to "does not
/// exist" - see <see cref="IReminderRepository.FindByIdAsync"/>.
/// <para>
/// <c>GET /household</c> is the one deliberate exception to "never see someone else's reminder":
/// it is a household-wide read, not a single-id lookup, and it goes through its own use case
/// (<see cref="GetHouseholdReminders"/>), its own repository call
/// (<see cref="IReminderRepository.ListVisibleForOthersInRangeAsync"/>) and its own response type
/// (<see cref="HouseholdReminderResponse"/>) rather than a wider <see cref="ReminderResponse"/>
/// with some fields hidden. <see cref="ReminderResponse"/> carries <c>Location</c> and
/// <c>TravelMinutes</c>; reusing it here would turn every future field added to it into a
/// potential leak to the rest of the household the moment nobody remembers to strip it back out
/// at this boundary. A narrower, separate DTO cannot leak a field it was never given.
/// </para>
/// <para>
/// Which rows <c>GET /household</c> even returns now depends on two things, not one: the
/// reminder's <see cref="ReminderVisibility"/> (WHAT the household may see, unchanged) and its
/// <c>Reminder.Audience</c> (WHO among them - a caller not named in a
/// <c>ReminderAudience.Selected</c> reminder's shares never sees that row at all, same as if it
/// were <see cref="ReminderVisibility.Private"/>). Both checks already happen inside
/// <see cref="IReminderRepository.ListVisibleForOthersInRangeAsync"/>, so this endpoint and
/// <see cref="GetHouseholdReminders"/> stay exactly as thin as before - see that method's own
/// remarks for the fail-closed guarantee behind it.
/// </para>
/// </remarks>
internal static class ReminderEndpoints
{
    internal static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        var reminders = app.MapGroup("/api/households/{householdId:guid}/reminders")
            .WithTags("Reminders")
            .RequireAuthorization()
            .AddEndpointFilter<HouseholdAccessFilter>();

        reminders.MapGet("/", ListOwnRemindersAsync)
            .Produces<IReadOnlyList<ReminderResponse>>()
            .ProducesValidationProblem();

        reminders.MapGet("/household", ListHouseholdRemindersAsync)
            .Produces<IReadOnlyList<HouseholdReminderResponse>>()
            .ProducesValidationProblem();

        reminders.MapPost("/", CreateReminderAsync)
            .Produces<ReminderResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        reminders.MapPut("/{reminderId:guid}/title", ChangeReminderTitleAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        reminders.MapPut("/{reminderId:guid}/location", ChangeReminderLocationAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        reminders.MapPut("/{reminderId:guid}/visibility", SetReminderVisibilityAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        reminders.MapPut("/{reminderId:guid}/audience", SetReminderAudienceAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        reminders.MapPut("/{reminderId:guid}/move", MoveReminderAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status400BadRequest)
            .ProducesValidationProblem();

        reminders.MapPut("/{reminderId:guid}/travel-minutes", SetReminderTravelMinutesAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        reminders.MapPost("/{reminderId:guid}/cancel", CancelReminderAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        reminders.MapPost("/{reminderId:guid}/check-off", CheckOffReminderAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        reminders.MapPost("/{reminderId:guid}/restore", RestoreReminderAsync)
            .Produces<ReminderResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ListOwnRemindersAsync(
        Guid householdId,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        GetOwnReminders getOwnReminders,
        CancellationToken cancellationToken)
    {
        if (from is null || to is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Range"] = ["Både from och to måste anges."]
            });
        }

        // Always the caller's own reminders - see this file's own remarks. There is no
        // memberId route parameter to ask for someone else's.
        var membership = httpContext.GetMembership();

        var found = await getOwnReminders.HandleAsync(
            householdId, membership.MemberId, from.Value, to.Value, cancellationToken);

        return Results.Ok(found.Select(ToResponse).ToList());
    }

    private static async Task<IResult> ListHouseholdRemindersAsync(
        Guid householdId,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        GetHouseholdReminders getHouseholdReminders,
        CancellationToken cancellationToken)
    {
        if (from is null || to is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Range"] = ["Både from och to måste anges."]
            });
        }

        // The caller's own id is only used to EXCLUDE their own reminders here - see
        // GetHouseholdReminders and IReminderRepository.ListVisibleForOthersInRangeAsync - not to
        // scope what they may see, unlike every other route in this file.
        var membership = httpContext.GetMembership();

        var found = await getHouseholdReminders.HandleAsync(
            householdId, membership.MemberId, from.Value, to.Value, cancellationToken);

        return Results.Ok(found.Select(ToHouseholdResponse).ToList());
    }

    private static async Task<IResult> CreateReminderAsync(
        Guid householdId,
        HttpContext httpContext,
        CreateReminderRequest request,
        CreateReminder createReminder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = ["En påminnelse måste ha en titel."]
            });
        }

        if (request.Date is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Ett datum måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        var reminder = await createReminder.HandleAsync(
            householdId,
            membership.MemberId,
            request.Title,
            request.Location,
            request.Date.Value,
            request.TimeOfDay,
            request.TravelMinutes,
            request.Visibility,
            request.Audience,
            request.MemberIds,
            cancellationToken);

        return reminder is null
            ? Results.NotFound()
            : Results.Created($"/api/households/{householdId}/reminders", ToResponse(reminder));
    }

    private static async Task<IResult> ChangeReminderTitleAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        ChangeReminderTitleRequest request,
        ChangeReminderTitle changeReminderTitle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Title)] = ["En påminnelse måste ha en titel."]
            });
        }

        var membership = httpContext.GetMembership();

        var reminder = await changeReminderTitle.HandleAsync(
            householdId, membership.MemberId, reminderId, request.Title, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> ChangeReminderLocationAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        ChangeReminderLocationRequest request,
        ChangeReminderLocation changeReminderLocation,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var reminder = await changeReminderLocation.HandleAsync(
            householdId, membership.MemberId, reminderId, request.Location, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> SetReminderVisibilityAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        SetReminderVisibilityRequest request,
        ChangeReminderVisibility changeReminderVisibility,
        CancellationToken cancellationToken)
    {
        if (request.Visibility is not { } visibility)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Visibility)] = ["En nivå måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        var reminder = await changeReminderVisibility.HandleAsync(
            householdId, membership.MemberId, reminderId, visibility, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> SetReminderAudienceAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        SetReminderAudienceRequest request,
        SetReminderAudience setReminderAudience,
        CancellationToken cancellationToken)
    {
        if (request.Audience is not { } audience)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Audience)] = ["En mottagargrupp måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        var reminder = await setReminderAudience.HandleAsync(
            householdId, membership.MemberId, reminderId, audience, request.MemberIds ?? [], cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> MoveReminderAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        MoveReminderRequest request,
        MoveReminder moveReminder,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Ett datum måste anges."]
            });
        }

        var membership = httpContext.GetMembership();

        var reminder = await moveReminder.HandleAsync(
            householdId, membership.MemberId, reminderId, request.Date.Value, request.TimeOfDay, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> SetReminderTravelMinutesAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        SetReminderTravelMinutesRequest request,
        SetReminderTravelMinutes setReminderTravelMinutes,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var reminder = await setReminderTravelMinutes.HandleAsync(
            householdId, membership.MemberId, reminderId, request.TravelMinutes, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> CancelReminderAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        CancelReminder cancelReminder,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var reminder = await cancelReminder.HandleAsync(
            householdId, membership.MemberId, reminderId, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> CheckOffReminderAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        CheckOffReminder checkOffReminder,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var reminder = await checkOffReminder.HandleAsync(
            householdId, membership.MemberId, reminderId, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static async Task<IResult> RestoreReminderAsync(
        Guid householdId,
        Guid reminderId,
        HttpContext httpContext,
        RestoreReminder restoreReminder,
        CancellationToken cancellationToken)
    {
        var membership = httpContext.GetMembership();

        var reminder = await restoreReminder.HandleAsync(
            householdId, membership.MemberId, reminderId, cancellationToken);

        return reminder is null ? Results.NotFound() : Results.Ok(ToResponse(reminder));
    }

    private static ReminderResponse ToResponse(Reminder reminder)
        => new(
            reminder.Id,
            reminder.Title,
            reminder.Location,
            reminder.Date,
            reminder.TimeOfDay,
            reminder.TravelMinutes,
            reminder.Status,
            reminder.CreatedAt,
            reminder.Visibility,
            reminder.Audience,
            [.. reminder.Shares.Select(share => share.MemberId)]);

    private static HouseholdReminderResponse ToHouseholdResponse(HouseholdReminderView view)
        => new(view.Id, view.MemberId, view.Date, view.TimeOfDay, view.Title);
}
