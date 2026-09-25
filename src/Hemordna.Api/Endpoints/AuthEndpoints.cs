using System.Net;
using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Application.Authentication;
using Hemordna.Application.Households;
using Hemordna.Infrastructure.Email;
using Hemordna.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hemordna.Api.Endpoints;

internal static class AuthEndpoints
{
    internal static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous()
            .RequireRateLimiting("auth");

        auth.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .Produces<AccessTokenResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        // Same anonymous group, same "auth" rate-limit policy as /login and /register - a
        // refresh call cannot require a bearer token (the whole point is that the access token
        // may already have expired), so it has exactly the same abuse surface as a password
        // login: an attacker submitting guesses as fast as the API allows. See RefreshAsync's
        // remarks for why every rejection reason looks identical from here.
        auth.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .Produces(StatusCodes.Status200OK);

        auth.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        // Anonymous, same group as /refresh - deliberately not RequireAuthorization(). See
        // RevokeRefreshTokenChain's remarks: possessing the raw refresh token is already enough
        // to act as its owner (it can be rotated into a fresh access token), so this needs no
        // separate proof of identity, and a caller whose access token already expired can still
        // log out - the exact moment logging out matters most.
        auth.MapPost("/logout", LogoutAsync)
            .WithName("Logout")
            .Produces(StatusCodes.Status200OK);

        app.MapGet("/api/me", GetMeAsync)
            .WithName("GetMe")
            .RequireAuthorization()
            .WithTags("Auth")
            .Produces<MeResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/auth/change-password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .RequireAuthorization()
            .RequireRateLimiting("auth")
            .WithTags("Auth")
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<HemordnaUser> users,
        JwtTokenIssuer tokens,
        IssueRefreshToken issueRefreshToken,
        IOptions<JwtOptions> jwtOptions,
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

        var response = await IssueAccessTokenResponseAsync(
            user, tokens, issueRefreshToken, jwtOptions, cancellationToken);

        return Results.Created("/api/me", response);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<HemordnaUser> users,
        SignInManager<HemordnaUser> signIn,
        JwtTokenIssuer tokens,
        IssueRefreshToken issueRefreshToken,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Unauthorized();
        }

        var user = await users.FindByEmailAsync(request.Email);

        // The same response whether the address is unknown or the password is wrong, so the
        // endpoint cannot be used to find out which e-mail addresses are registered.
        if (user is null || !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
        {
            return Results.Unauthorized();
        }

        var response = await IssueAccessTokenResponseAsync(
            user, tokens, issueRefreshToken, jwtOptions, cancellationToken);

        return Results.Ok(response);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        RotateRefreshToken rotate,
        UserManager<HemordnaUser> users,
        JwtTokenIssuer tokens,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.Unauthorized();
        }

        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays);
        var rotated = await rotate.HandleAsync(request.RefreshToken, refreshLifetime, cancellationToken);

        // Unknown, expired, already consumed (reuse), already revoked, or a lost race - see
        // RotateRefreshToken.HandleAsync's remarks. Every one of those must look identical from
        // here: the same 401, with no clue which case it was.
        if (rotated is null || await users.FindByIdAsync(rotated.UserId.ToString()) is not { } user)
        {
            return Results.Unauthorized();
        }

        var accessToken = tokens.Issue(user);

        return Results.Ok(new AccessTokenResponse(
            accessToken.Token, accessToken.ExpiresAt, rotated.Token, rotated.ExpiresAt));
    }

    private static async Task<IResult> LogoutAsync(RefreshTokenRequest request, RevokeRefreshTokenChain revoke, CancellationToken cancellationToken)
    {
        // A blank value is treated the same as any other unrecognized token - see
        // RevokeRefreshTokenChain.HandleAsync's remarks: this endpoint never reveals whether a
        // presented value meant anything.
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await revoke.HandleAsync(request.RefreshToken, cancellationToken);
        }

        return Results.Ok();
    }

    internal static async Task<AccessTokenResponse> IssueAccessTokenResponseAsync(
        HemordnaUser user,
        JwtTokenIssuer tokens,
        IssueRefreshToken issueRefreshToken,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        var accessToken = tokens.Issue(user);
        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays);
        var refreshToken = await issueRefreshToken.HandleAsync(user.Id, refreshLifetime, cancellationToken);

        return new AccessTokenResponse(
            accessToken.Token, accessToken.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt);
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
        UserManager<HemordnaUser> users,
        RevokeAllRefreshTokensForUser revokeAllRefreshTokens,
        CancellationToken cancellationToken)
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

        // Reset-password is the flow used when someone believes the account has been accessed
        // by somebody else - a refresh token that survives it would hand an attacker 60 more
        // days despite the user having done the one thing they knew to do about it. Unlike
        // ChangePasswordAsync below, no replacement chain is issued here: this request arrives
        // through an e-mailed link with no session of its own to speak of, so there is no
        // "device you are sitting at" to carry forward - everything is meant to die, on purpose.
        await revokeAllRefreshTokens.HandleAsync(user.Id, cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        HttpContext httpContext,
        UserManager<HemordnaUser> users,
        JwtTokenIssuer tokens,
        RevokeAllRefreshTokensForUser revokeAllRefreshTokens,
        IssueRefreshToken issueRefreshToken,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
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

        // Every refresh token this user has is revoked first - including the one belonging to
        // this very request - and then a brand new chain is issued to the device making this
        // call. That is deliberate, not an oversight: changing your password signs out every
        // OTHER device, but not the one you are sitting at, since you have just proven both the
        // old password and the new one in this same request - signing yourself out too would
        // protect nothing. Contrast ResetPasswordAsync just above: that flow is reached through
        // an e-mailed link with no session at all, used precisely when someone suspects a
        // different device is the problem, so everything is meant to die there - no replacement
        // chain is issued.
        await revokeAllRefreshTokens.HandleAsync(userId, cancellationToken);
        var refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays);
        var refreshToken = await issueRefreshToken.HandleAsync(userId, refreshLifetime, cancellationToken);

        var token = tokens.Issue(user);
        return Results.Ok(new AccessTokenResponse(token.Token, token.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt));
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
            membership?.MemberId,
            membership?.CanManageHousehold ?? false));
    }
}
