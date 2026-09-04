using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Households;
using Hemordna.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hemordna.Api.Endpoints;

internal static class AuthEndpoints
{
    internal static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<AccessTokenResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapGet("/api/me", GetMeAsync)
            .WithName("GetMe")
            .RequireAuthorization()
            .WithTags("Auth")
            .Produces<MeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<HemordnaUser> users,
        JwtTokenIssuer tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["E-post, lösenord och visningsnamn krävs."]
            });
        }

        var user = new HemordnaUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName.Trim()
        };

        var result = await users.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            // Identity's own messages cover password rules and duplicate e-mail.
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray()));
        }

        var token = tokens.Issue(user);

        return Results.Created("/api/me", new AccessTokenResponse(token.Token, token.ExpiresAt));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<HemordnaUser> users,
        JwtTokenIssuer tokens)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByEmailAsync(request.Email);

        // The same response whether the address is unknown or the password is wrong, so the
        // endpoint cannot be used to find out which e-mail addresses are registered.
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        var token = tokens.Issue(user);

        return Results.Ok(new AccessTokenResponse(token.Token, token.ExpiresAt));
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext httpContext,
        UserManager<HemordnaUser> users,
        IHouseholdMembershipQuery memberships,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var membership = await memberships.FindByUserIdAsync(userId, cancellationToken);

        return Results.Ok(new MeResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            membership?.HouseholdId,
            membership?.MemberId));
    }
}
