using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tasks;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// Transport for households and their contents. The endpoints map and delegate - all rules
/// live in the domain and the use cases.
/// </summary>
/// <remarks>
/// Everything below <c>/api/households/{householdId}</c> runs behind
/// <see cref="HouseholdAccessFilter"/>, so no handler has to remember the tenant check.
/// </remarks>
internal static class HouseholdEndpoints
{
    internal static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var households = app.MapGroup("/api/households")
            .WithTags("Households")
            .RequireAuthorization();

        households.MapPost("/", CreateAsync)
            .WithName("CreateHousehold")
            .Produces<HouseholdResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();

        // Household-scoped routes. The filter resolves and verifies membership.
        var scoped = households.MapGroup("/{householdId:guid}")
            .AddEndpointFilter<HouseholdAccessFilter>();

        scoped.MapGet("/", GetAsync)
            .WithName("GetHousehold")
            .Produces<HouseholdResponse>()
            .Produces(StatusCodes.Status404NotFound);

        scoped.MapPost("/members", AddMemberAsync)
            .Produces<HouseholdMemberResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        scoped.MapPost("/areas", AddAreaAsync)
            .Produces<AreaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        scoped.MapGet("/tasks", ListTasksAsync)
            .Produces<IReadOnlyList<TaskDefinitionResponse>>();

        scoped.MapPost("/tasks", CreateTaskAsync)
            .Produces<TaskDefinitionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        scoped.MapPost("/tasks/{taskId:guid}/occurrences", ScheduleOccurrenceAsync)
            .Produces<TaskOccurrenceResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound);

        scoped.MapPut("/members/{memberId:guid}/availability", SetAvailabilityAsync)
            .Produces<AvailabilityResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        scoped.MapPut("/members/{memberId:guid}/weekly-budget", SetWeeklyBudgetAsync)
            .Produces<HouseholdMemberResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        scoped.MapGet("/members/{memberId:guid}/preferences", GetPreferenceAsync)
            .Produces<PreferenceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        scoped.MapPut("/members/{memberId:guid}/preferences", SetPreferenceAsync)
            .Produces<PreferenceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        scoped.MapPost("/occurrences/{occurrenceId:guid}/complete", CompleteOccurrenceAsync)
            .Produces<TaskOccurrenceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        scoped.MapPost("/occurrences/{occurrenceId:guid}/defer", DeferOccurrenceAsync)
            .Produces<TaskOccurrenceResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        scoped.MapGet("/activity", GetRecentActivityAsync)
            .Produces<IReadOnlyList<RecentActivityResponse>>();

        scoped.MapGet("/members/{memberId:guid}/plan", GetPlanAsync)
            .WithName("GetDailyPlan")
            .Produces<DailyPlanResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateHouseholdRequest request,
        HttpContext httpContext,
        CreateHousehold createHousehold,
        IHouseholdMembershipQuery memberships,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Ett hushåll måste ha ett namn."]
            });
        }

        // One user belongs to one household for now. Creating a second would leave the user
        // with two memberships and no way to say which one a request means.
        if (await memberships.FindByUserIdAsync(userId, cancellationToken) is not null)
        {
            return Results.Conflict(new { message = "Användaren tillhör redan ett hushåll." });
        }

        var displayName = httpContext.User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["DisplayName"] = ["Inloggningen saknar visningsnamn."]
            });
        }

        var household = await createHousehold.HandleAsync(
            request.Name, userId, displayName, cancellationToken);

        return Results.CreatedAtRoute(
            "GetHousehold",
            new { householdId = household.Id },
            ToResponse(household));
    }

    private static async Task<IResult> GetAsync(
        Guid householdId,
        GetHousehold getHousehold,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(householdId, cancellationToken);

        return household is null ? Results.NotFound() : Results.Ok(ToResponse(household));
    }

    private static async Task<IResult> AddMemberAsync(
        Guid householdId,
        AddMemberRequest request,
        AddHouseholdMember addMember,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.DisplayName)] = ["En medlem måste ha ett namn."]
            });
        }

        var budget = request.WeeklyTimeBudgetMinutes?.ToDomain() ?? WeeklyTimeBudget.Empty;

        var member = await addMember.HandleAsync(
            householdId, request.DisplayName, budget, cancellationToken);

        return member is null
            ? Results.NotFound()
            : Results.Created($"/api/households/{householdId}", ToResponse(member));
    }

    private static async Task<IResult> AddAreaAsync(
        Guid householdId,
        AddAreaRequest request,
        AddArea addArea,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Ett område måste ha ett namn."]
            });
        }

        var area = await addArea.HandleAsync(householdId, request.Name, cancellationToken);

        return area is null
            ? Results.NotFound()
            : Results.Created($"/api/households/{householdId}", ToResponse(area));
    }

    private static async Task<IResult> ListTasksAsync(
        Guid householdId,
        ITaskDefinitionRepository definitions,
        CancellationToken cancellationToken)
    {
        var tasks = await definitions.ListByHouseholdAsync(householdId, cancellationToken);

        return Results.Ok(tasks.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateTaskAsync(
        Guid householdId,
        CreateTaskRequest request,
        CreateTaskDefinition createTask,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["En uppgift måste ha ett namn."]
            });
        }

        if (request.EstimatedMinutes <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.EstimatedMinutes)] = ["Uppskattad tid måste vara större än noll."]
            });
        }

        var definition = await createTask.HandleAsync(
            householdId,
            new NewTaskDefinition(
                request.Name,
                request.EstimatedMinutes,
                request.Description,
                request.AreaId,
                request.Priority,
                request.DefaultResponsibleMemberId,
                request.PreferredWeekday,
                request.CanBeDeferred,
                request.HasRotatingResponsibility,
                request.RequiresMultiplePeople,
                request.Recurrence?.ToDomain()),
            cancellationToken);

        return definition is null
            ? Results.NotFound()
            : Results.Created($"/api/households/{householdId}/tasks", ToResponse(definition));
    }

    private static async Task<IResult> ScheduleOccurrenceAsync(
        Guid householdId,
        Guid taskId,
        ScheduleOccurrenceRequest request,
        ScheduleTaskOccurrence schedule,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Ett datum måste anges."]
            });
        }

        var occurrence = await schedule.HandleAsync(
            householdId, taskId, request.Date.Value, request.AssignToMemberId, cancellationToken);

        return occurrence is null
            ? Results.NotFound()
            : Results.Created($"/api/households/{householdId}/tasks/{taskId}", ToResponse(occurrence));
    }

    private static async Task<IResult> SetAvailabilityAsync(
        Guid householdId,
        Guid memberId,
        SetAvailabilityRequest request,
        SetMemberAvailability setAvailability,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Ett datum måste anges."]
            });
        }

        if (request.AvailableMinutes < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.AvailableMinutes)] = ["Tillgänglig tid kan inte vara negativ."]
            });
        }

        var availability = await setAvailability.HandleAsync(
            householdId, memberId, request.Date.Value, request.AvailableMinutes, cancellationToken);

        return availability is null
            ? Results.NotFound()
            : Results.Ok(new AvailabilityResponse(
                availability.MemberId, availability.Date, availability.AvailableMinutes));
    }

    private static async Task<IResult> SetWeeklyBudgetAsync(
        Guid householdId,
        Guid memberId,
        WeeklyTimeBudgetContract request,
        SetMemberWeeklyBudget setWeeklyBudget,
        CancellationToken cancellationToken)
    {
        var member = await setWeeklyBudget.HandleAsync(
            householdId, memberId, request.ToDomain(), cancellationToken);

        return member is null ? Results.NotFound() : Results.Ok(ToResponse(member));
    }

    private static async Task<IResult> GetPreferenceAsync(
        Guid householdId,
        Guid memberId,
        GetMemberPreference getPreference,
        CancellationToken cancellationToken)
    {
        var preference = await getPreference.HandleAsync(householdId, memberId, cancellationToken);

        return preference is null
            ? Results.NotFound()
            : Results.Ok(new PreferenceResponse(preference.MemberId, preference.Presentation, preference.Motivation));
    }

    private static async Task<IResult> SetPreferenceAsync(
        Guid householdId,
        Guid memberId,
        SetPreferenceRequest request,
        SetMemberPreference setPreference,
        CancellationToken cancellationToken)
    {
        var preference = await setPreference.HandleAsync(
            householdId, memberId, request.Presentation, request.Motivation, cancellationToken);

        return preference is null
            ? Results.NotFound()
            : Results.Ok(new PreferenceResponse(preference.MemberId, preference.Presentation, preference.Motivation));
    }

    private static async Task<IResult> CompleteOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        HttpContext httpContext,
        CompleteTaskOccurrence complete,
        CancellationToken cancellationToken)
    {
        // The caller completes it as themselves. The membership was resolved and verified by
        // HouseholdAccessFilter, so it cannot name someone in another household.
        var membership = httpContext.GetMembership();

        var occurrence = await complete.HandleAsync(
            householdId, occurrenceId, membership.MemberId, cancellationToken);

        return occurrence is null ? Results.NotFound() : Results.Ok(ToResponse(occurrence));
    }

    private static async Task<IResult> DeferOccurrenceAsync(
        Guid householdId,
        Guid occurrenceId,
        DeferOccurrenceRequest request,
        DeferTaskOccurrence defer,
        CancellationToken cancellationToken)
    {
        if (request.Date is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Date)] = ["Ett datum att skjuta upp till måste anges."]
            });
        }

        var occurrence = await defer.HandleAsync(
            householdId, occurrenceId, request.Date.Value, cancellationToken);

        return occurrence is null ? Results.NotFound() : Results.Ok(ToResponse(occurrence));
    }

    private static async Task<IResult> GetRecentActivityAsync(
        Guid householdId,
        IRecentActivityQuery activity,
        CancellationToken cancellationToken)
    {
        var recent = await activity.FindRecentlyCompletedAsync(householdId, limit: 10, cancellationToken);

        return Results.Ok(recent
            .Select(a => new RecentActivityResponse(a.OccurrenceId, a.TaskName, a.MemberDisplayName, a.CompletedAt))
            .ToList());
    }

    private static async Task<IResult> GetPlanAsync(
        Guid householdId,
        Guid memberId,
        DateOnly? date,
        GetDailyPlan getDailyPlan,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // The date is a transport concern: if the caller does not name one, "today" is
        // resolved here at the boundary and passed in explicitly. Nothing below this line
        // reads a clock.
        var planDate = date ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var day = await getDailyPlan.HandleAsync(householdId, memberId, planDate, cancellationToken);

        return day is null ? Results.NotFound() : Results.Ok(ToResponse(day));
    }

    private static HouseholdResponse ToResponse(Household household)
        => new(
            household.Id,
            household.Name,
            household.CreatedAt,
            [.. household.Members.Select(ToResponse)],
            [.. household.Areas.Select(ToResponse)]);

    private static HouseholdMemberResponse ToResponse(HouseholdMember member)
        => new(
            member.Id,
            member.DisplayName,
            member.IsActive,
            WeeklyTimeBudgetContract.From(member.WeeklyTimeBudget));

    private static AreaResponse ToResponse(Area area) => new(area.Id, area.Name, area.IsActive);

    private static TaskDefinitionResponse ToResponse(TaskDefinition definition)
        => new(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.AreaId,
            definition.EstimatedMinutes,
            definition.Priority,
            definition.DefaultResponsibleMemberId,
            definition.PreferredWeekday,
            definition.CanBeDeferred,
            definition.HasRotatingResponsibility,
            definition.RequiresMultiplePeople,
            definition.IsActive,
            definition.Recurrence is { } recurrence ? RecurrenceRuleContract.From(recurrence) : null);

    private static TaskOccurrenceResponse ToResponse(TaskOccurrence occurrence)
        => new(
            occurrence.Id,
            occurrence.TaskDefinitionId,
            occurrence.ScheduledDate,
            occurrence.OriginalScheduledDate,
            occurrence.EstimatedMinutes,
            occurrence.Priority,
            occurrence.CanBeDeferred,
            occurrence.AssignedMemberId,
            occurrence.Status);

    private static DailyPlanResponse ToResponse(MemberDay day)
    {
        var plan = day.Plan;

        return new DailyPlanResponse(
            plan.MemberId,
            plan.Date,
            plan.AvailableMinutes,
            plan.PlannedMinutes,
            plan.RemainingMinutes,
            day.CompletedMinutes,
            [.. plan.Items.Select(item => new PlannedTaskResponse(
                item.Candidate.Occurrence.Id,
                item.Candidate.Occurrence.TaskDefinitionId,
                item.Candidate.TaskName,
                item.Candidate.EstimatedMinutes,
                item.Candidate.Priority,
                item.IsOverdue))],
            [.. day.Completed.Select(done => new CompletedTaskResponse(
                done.Occurrence.Id,
                done.Occurrence.TaskDefinitionId,
                done.TaskName,
                done.EstimatedMinutes))],
            [.. plan.Unplanned.Select(task => new UnplannedTaskResponse(
                task.Candidate.Occurrence.Id,
                task.Candidate.Occurrence.TaskDefinitionId,
                task.Candidate.TaskName,
                task.Candidate.EstimatedMinutes,
                task.Candidate.Priority,
                task.Candidate.CanBeDeferred,
                task.Reason))]);
    }
}
