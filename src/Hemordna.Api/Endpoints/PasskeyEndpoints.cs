using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Hemordna.Api.Authentication;
using Hemordna.Api.Contracts;
using Hemordna.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace Hemordna.Api.Endpoints;

/// <summary>
/// WebAuthn (passkeys - Face ID, Touch ID, Windows Hello) as an alternative to a password.
/// Registering one requires already being signed in (see <see cref="Program"/> for why this
/// group needs its own <c>RequireAuthorization</c>, same reasoning as ChangePassword in
/// AuthEndpoints); signing in with one is anonymous, same as a normal password login.
/// </summary>
/// <remarks>
/// Each WebAuthn ceremony is two calls - "give me a challenge" then "here is what the
/// authenticator did with it" - and the second call must be checked against the exact
/// challenge the first one issued. That state is too short-lived and per-flow to belong in the
/// database, so it lives in <see cref="IMemoryCache"/> for a few minutes instead - a single API
/// instance is enough for how this app is deployed (see docs/ARCHITECTURE.md).
/// </remarks>
internal static class PasskeyEndpoints
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    // Deliberately NOT the app's configured HTTP JSON options (ConfigureHttpJsonOptions in
    // Program.cs adds a global JsonStringEnumConverter for the app's own enums) - Fido2NetLib's
    // request/response types carry their own per-property [JsonConverter] attributes (matching
    // the WebAuthn spec's exact wire values, e.g. "public-key") and a plain, unmodified options
    // instance is what lets those win instead of getting shadowed by the global one.
    private static readonly JsonSerializerOptions Fido2JsonOptions = new();

    internal static IEndpointRouteBuilder MapPasskeyEndpoints(this IEndpointRouteBuilder app)
    {
        var passkeys = app.MapGroup("/api/auth/passkeys").WithTags("Auth");

        passkeys.MapGet("/", ListPasskeysAsync)
            .WithName("ListPasskeys")
            .RequireAuthorization()
            .Produces<IReadOnlyList<PasskeyResponse>>()
            .Produces(StatusCodes.Status401Unauthorized);

        passkeys.MapPost("/register/options", GetRegisterOptionsAsync)
            .WithName("GetPasskeyRegisterOptions")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        passkeys.MapPost("/register/verify", VerifyRegisterAsync)
            .WithName("VerifyPasskeyRegister")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status401Unauthorized);

        passkeys.MapDelete("/{credentialId}", DeletePasskeyAsync)
            .WithName("DeletePasskey")
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        passkeys.MapPost("/login/options", GetLoginOptionsAsync)
            .WithName("GetPasskeyLoginOptions")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK);

        passkeys.MapPost("/login/verify", VerifyLoginAsync)
            .WithName("VerifyPasskeyLogin")
            .AllowAnonymous()
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> ListPasskeysAsync(
        HttpContext httpContext,
        IPasskeyCredentialStore store,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        var credentials = await store.ListByUserIdAsync(userId, cancellationToken);

        return Results.Ok(credentials
            .Select(credential => new PasskeyResponse(
                Base64UrlText.Encode(credential.CredentialId), credential.DeviceLabel, credential.CreatedAt))
            .ToList());
    }

    private static async Task<IResult> GetRegisterOptionsAsync(
        HttpContext httpContext,
        UserManager<HemordnaUser> users,
        IPasskeyCredentialStore store,
        IFido2 fido2,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId
            || await users.FindByIdAsync(userId.ToString()) is not { } user)
        {
            return Results.Unauthorized();
        }

        var excludeCredentials = (await store.ListByUserIdAsync(userId, cancellationToken))
            .Select(credential => new PublicKeyCredentialDescriptor(credential.CredentialId))
            .ToList();

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = userId.ToByteArray(),
                Name = user.Email ?? user.Id.ToString(),
                DisplayName = user.DisplayName
            },
            ExcludeCredentials = excludeCredentials,
            // Required, not merely preferred: a resident/discoverable credential is what lets
            // GetLoginOptionsAsync skip asking for an e-mail address up front - the browser can
            // list "who has a passkey for this site" itself. Every platform authenticator in
            // real use (iCloud Keychain, Windows Hello, Android) supports this.
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None,
            PubKeyCredParams = PubKeyCredParam.Defaults
        });

        cache.Set(RegisterCacheKey(userId), options, ChallengeLifetime);

        // Fido2NetLib's own ToJson(), not Results.Ok(options): the default ASP.NET Core
        // serializer has no idea these enums must be lowercase per the WebAuthn spec ("none",
        // not "None") - Results.Ok(options) silently sends values Chrome refuses to recognize.
        return Results.Text(options.ToJson(), "application/json");
    }

    private static async Task<IResult> VerifyRegisterAsync(
        HttpContext httpContext,
        UserManager<HemordnaUser> users,
        IPasskeyCredentialStore store,
        IFido2 fido2,
        IMemoryCache cache,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId
            || await users.FindByIdAsync(userId.ToString()) is null)
        {
            return Results.Unauthorized();
        }

        if (!cache.TryGetValue(RegisterCacheKey(userId), out CredentialCreateOptions? options) || options is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["Registreringen har gått ut. Försök igen."]
            });
        }

        cache.Remove(RegisterCacheKey(userId));

        AuthenticatorAttestationRawResponse? attestation;
        try
        {
            attestation = await JsonSerializer.DeserializeAsync<AuthenticatorAttestationRawResponse>(
                httpContext.Request.Body, Fido2JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            attestation = null;
        }

        if (attestation is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["Registreringen har gått ut. Försök igen."]
            });
        }

        try
        {
            var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (parameters, ct) =>
                    !await store.ExistsAsync(parameters.CredentialId, ct)
            }, cancellationToken);

            await store.AddAsync(new PasskeyCredential
            {
                CredentialId = result.Id,
                UserId = userId,
                PublicKey = result.PublicKey,
                SignCount = result.SignCount,
                DeviceLabel = GuessDeviceLabel(httpContext.Request.Headers.UserAgent.ToString()),
                CreatedAt = timeProvider.GetUtcNow()
            }, cancellationToken);

            return Results.Ok();
        }
        catch (Fido2VerificationException)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Request"] = ["Det gick inte att registrera enheten. Försök igen."]
            });
        }
    }

    private static async Task<IResult> DeletePasskeyAsync(
        string credentialId,
        HttpContext httpContext,
        IPasskeyCredentialStore store,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.GetUserId() is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (!Base64UrlText.TryDecode(credentialId, out var rawId))
        {
            return Results.NotFound();
        }

        return await store.RemoveAsync(rawId, userId, cancellationToken) ? Results.Ok() : Results.NotFound();
    }

    /// <summary>
    /// No e-mail, no username - an empty allow-list tells the browser to show whatever
    /// discoverable passkey it has for this site itself. Anonymous by nature: there is nothing
    /// yet to say "unauthorized" about.
    /// </summary>
    private static IResult GetLoginOptionsAsync(IFido2 fido2, IMemoryCache cache)
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred
        });

        // The flow id (not an e-mail address, not the user - neither is known yet) is what
        // lets VerifyLoginAsync find the exact challenge this call issued.
        var flowId = Guid.NewGuid().ToString("N");
        cache.Set(LoginCacheKey(flowId), options, ChallengeLifetime);

        // A hand-built envelope, not a bound response type: options.ToJson() (see the identical
        // comment in GetRegisterOptionsAsync) already returns a complete, correctly-shaped JSON
        // string, so wrapping it in a small string template - rather than parsing it back into
        // an object just to re-serialize it - keeps that shape untouched.
        return Results.Text($$"""{"flowId":"{{flowId}}","options":{{options.ToJson()}}}""", "application/json");
    }

    private static async Task<IResult> VerifyLoginAsync(
        string? flowId,
        HttpContext httpContext,
        UserManager<HemordnaUser> users,
        IPasskeyCredentialStore store,
        IFido2 fido2,
        JwtTokenIssuer tokens,
        IMemoryCache cache,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flowId)
            || !cache.TryGetValue(LoginCacheKey(flowId), out AssertionOptions? options)
            || options is null)
        {
            return Results.Unauthorized();
        }

        cache.Remove(LoginCacheKey(flowId));

        AuthenticatorAssertionRawResponse? assertion;
        try
        {
            assertion = await JsonSerializer.DeserializeAsync<AuthenticatorAssertionRawResponse>(
                httpContext.Request.Body, Fido2JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            assertion = null;
        }

        // The credential id in the assertion is how the person is identified at all here - no
        // e-mail or username ever enters this flow. See the same reasoning in LoginAsync for
        // why an unknown credential and a wrong password look identical to the caller.
        if (assertion is null
            || await store.FindByCredentialIdAsync(assertion.RawId, cancellationToken) is not { } credential
            || await users.FindByIdAsync(credential.UserId.ToString()) is not { } user)
        {
            return Results.Unauthorized();
        }

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertion,
                OriginalOptions = options,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = async (parameters, ct) =>
                    parameters.UserHandle is not { Length: > 0 }
                    || (await store.FindByCredentialIdAsync(parameters.CredentialId, ct))?.UserId
                        .ToByteArray().AsSpan().SequenceEqual(parameters.UserHandle) is true
            }, cancellationToken);

            await store.UpdateSignCountAsync(result.CredentialId, result.SignCount, cancellationToken);

            var token = tokens.Issue(user);
            return Results.Ok(new AccessTokenResponse(token.Token, token.ExpiresAt));
        }
        catch (Fido2VerificationException)
        {
            return Results.Unauthorized();
        }
    }

    private static string RegisterCacheKey(Guid userId) => $"passkey-register:{userId}";

    private static string LoginCacheKey(string flowId) => $"passkey-login:{flowId}";

    /// <summary>A rough, cosmetic label so a person can tell their registered passkeys apart -
    /// never used for anything security-relevant.</summary>
    private static string GuessDeviceLabel(string userAgent) => userAgent switch
    {
        _ when userAgent.Contains("iPhone") => "iPhone",
        _ when userAgent.Contains("iPad") => "iPad",
        _ when userAgent.Contains("Android") => "Android-enhet",
        _ when userAgent.Contains("Macintosh") => "Mac",
        _ when userAgent.Contains("Windows") => "Windows-dator",
        _ => "Okänd enhet"
    };
}

/// <summary>The URL-safe, unpadded base64 WebAuthn credential ids already use elsewhere in the
/// ceremony (see Fido2NetLib's own Base64UrlConverter) - reused here only so one can travel as
/// a route value in DeletePasskeyAsync and as PasskeyResponse.Id.</summary>
internal static class Base64UrlText
{
    internal static string Encode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static bool TryDecode(string text, out byte[] bytes)
    {
        try
        {
            var padded = text.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
