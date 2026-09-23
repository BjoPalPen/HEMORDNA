using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Reminders;
using Hemordna.Domain.Reminders;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// Transport for a member's own reminders - see docs/PRODUCT.md §11. The endpoints map and
/// delegate; every rule (including the privacy boundary) lives in the domain and the use cases.
/// </summary>
/// <remarks>
/// All routes run behind <see cref="HouseholdAccessFilter"/>, same as
/// <see cref="HouseholdEndpoints"/>'s <c>scoped</c> group. Deliberately not on <c>manage</c> - a
/// reminder is not household configuration, every member handles their own - and not on
/// <c>selfOnly</c> either, since there is no <c>memberId</c> route parameter to check: the owner
/// is always <c>httpContext.GetMembership().MemberId</c>, the same pattern
/// <c>GetTimeCreditAsync</c> and <c>CreateExtraTaskAsync</c> already use for "always the caller's
/// own". A caller can therefore never name another member's reminder, and every use case already
/// treats "belongs to someone else" identically to "does not exist" - see
/// <see cref="IReminderRepository.FindByIdAsync"/>.
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
            reminder.CreatedAt);
}
