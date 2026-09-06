using System.Net;
using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Households;
using Hemordna.Infrastructure.Email;
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

        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .Produces(StatusCodes.Status200OK);

        auth.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        app.MapGet("/api/me", GetMeAsync)
            .WithName("GetMe")
            .RequireAuthorization()
            .WithTags("Auth")
            .Produces<MeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/auth/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .WithTags("Auth")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
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

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<HemordnaUser> users,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Email)
            && await users.FindByEmailAsync(request.Email) is { Email: { } email } user)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var publicUrl = configuration["App:PublicUrl"]?.TrimEnd('/') ?? "https://app.hemordna.se";
            var link = $"{publicUrl}/aterstall-losenord?email={WebUtility.UrlEncode(email)}&token={WebUtility.UrlEncode(token)}";

            try
            {
                await emailSender.SendAsync(
                    email,
                    "Återställ ditt lösenord – Hemordna",
                    $"""
                    <p>Hej {WebUtility.HtmlEncode(user.DisplayName)},</p>
                    <p>Klicka på länken nedan för att välja ett nytt lösenord till Hemordna:</p>
                    <p><a href="{link}">{link}</a></p>
                    <p>Bad du inte om detta kan du bortse från mejlet.</p>
                    """,
                    cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                // The response below must look identical either way - see the comment on it -
                // so a delivery failure is logged, not surfaced to the caller.
                logger.LogError(exception, "Failed to send password-reset e-mail.");
            }
        }

        // Same response whether the address is registered or not, and regardless of whether
        // sending succeeded - otherwise this endpoint could be used to find out which e-mail
        // addresses have an account, or to probe for delivery failures.
        return Results.Ok();
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<HemordnaUser> users)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.Token)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["E-post, token och nytt lösenord krävs."]
            });
        }

        if (await users.FindByEmailAsync(request.Email) is not { } user)
        {
            // Same message as an invalid or expired token - see ForgotPasswordAsync.
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Token"] = ["Länken är ogiltig eller har gått ut. Begär en ny."]
            });
        }

        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray()));
        }

        return Results.Ok();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<HemordnaUser> users)
    {
        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["Nuvarande och nytt lösenord krävs."]
            });
        }

        if (await users.FindByIdAsync(userId.ToString()) is not { } user)
        {
            return Results.Unauthorized();
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray()));
        }

        return Results.Ok();
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
