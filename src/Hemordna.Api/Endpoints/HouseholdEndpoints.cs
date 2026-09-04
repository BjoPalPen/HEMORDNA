using Hemordna.Api.Contracts;
using Hemordna.Application.Households;
using Hemordna.Domain.Households;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// Transport for households. The endpoints map and delegate - all rules live in the domain
/// and the use cases.
/// </summary>
internal static class HouseholdEndpoints
{
    internal static IEndpointRouteBuilder MapHouseholdEndpoints(this IEndpointRouteBuilder app)
    {
        var households = app.MapGroup("/api/households").WithTags("Households");

        households.MapPost("/", CreateAsync)
            .WithName("CreateHousehold")
            .Produces<HouseholdResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        households.MapGet("/{id:guid}", GetAsync)
            .WithName("GetHousehold")
            .Produces<HouseholdResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateHouseholdRequest request,
        CreateHousehold createHousehold,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.Name)] = ["Ett hushåll måste ha ett namn."]
            });
        }

        var household = await createHousehold.HandleAsync(request.Name, cancellationToken);

        return Results.CreatedAtRoute(
            "GetHousehold",
            new { id = household.Id },
            ToResponse(household));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GetHousehold getHousehold,
        CancellationToken cancellationToken)
    {
        var household = await getHousehold.HandleAsync(id, cancellationToken);

        return household is null ? Results.NotFound() : Results.Ok(ToResponse(household));
    }

    private static HouseholdResponse ToResponse(Household household)
        => new(
            household.Id,
            household.Name,
            household.CreatedAt,
            [.. household.Members.Select(m => new HouseholdMemberResponse(m.Id, m.DisplayName, m.IsActive))],
            [.. household.Areas.Select(a => new AreaResponse(a.Id, a.Name, a.IsActive))]);
}
