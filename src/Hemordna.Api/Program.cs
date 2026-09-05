using System.Text;
using System.Text.Json.Serialization;
using Hemordna.Api;
using Hemordna.Api.Authentication;
using Hemordna.Api.Endpoints;
using Hemordna.Api.Realtime;
using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Realtime;
using Hemordna.Application.Tasks;
using Hemordna.Infrastructure;
using Hemordna.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Enums travel as names, not numbers. A client reading "ExceedsRemainingTime" needs no
// lookup table, and inserting an enum member later cannot silently change what a value means.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddInfrastructure(builder.Configuration);

// TimeProvider rather than a static clock, so use cases stay testable.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<CreateHousehold>();
builder.Services.AddScoped<JoinHousehold>();
builder.Services.AddScoped<RegenerateInviteCode>();
builder.Services.AddScoped<GetHousehold>();
builder.Services.AddScoped<AddHouseholdMember>();
builder.Services.AddScoped<AddArea>();
builder.Services.AddScoped<DeactivateArea>();
builder.Services.AddScoped<DeactivateHouseholdMember>();
builder.Services.AddScoped<SetMemberAvailability>();
builder.Services.AddScoped<SetMemberWeeklyBudget>();
builder.Services.AddScoped<SetMemberRole>();
builder.Services.AddScoped<SetMemberPreference>();
builder.Services.AddScoped<GetMemberPreference>();
builder.Services.AddScoped<CreateTaskDefinition>();
builder.Services.AddScoped<DeactivateTaskDefinition>();
builder.Services.AddScoped<ScheduleTaskOccurrence>();
builder.Services.AddScoped<CompleteTaskOccurrence>();
builder.Services.AddScoped<DeferTaskOccurrence>();
builder.Services.AddScoped<EnsureOccurrencesGenerated>();
builder.Services.AddScoped<GetDailyPlan>();
builder.Services.AddSingleton<DailyPlanner>();

// SignalR pushes changes to a household's other connected clients - see docs/ARCHITECTURE.md §5.
builder.Services.AddSignalR();
builder.Services.AddScoped<IHouseholdNotifier, SignalRHouseholdNotifier>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtTokenIssuer>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // A browser cannot set an Authorization header on the WebSocket handshake SignalR
        // uses, so the token travels as a query string parameter on that one path instead.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs")
                    && context.Request.Query.TryGetValue("access_token", out var accessToken))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// The Blazor client is served from its own origin, so it needs an explicit allowance.
// Origins come from configuration - no wildcard, because the API accepts credentials.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<HemordnaDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await DevelopmentDataSeeder.SeedAsync(app.Services);
}

// Off by default - the runtime image has no dotnet-ef CLI, so this is how migrations reach
// a deployed database instead. Opt in per environment (RUN_MIGRATIONS_ON_STARTUP=true) rather
// than always-on, so a deploy never applies a migration by surprise.
if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider.GetRequiredService<HemordnaDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// In production the published Blazor client's wwwroot is copied into this project's own
// wwwroot (see Dockerfile), so one container serves both the API and the SPA - Caddy then
// only needs a single upstream. In development the client runs as its own dev-server
// process instead, so wwwroot is absent here and these are harmless no-ops.
app.UseDefaultFiles();

// The default ContentTypeProvider has no mapping for .dat/.blat (ICU globalization data,
// the Blazor lazy-loading manifest) or .wasm - ServeUnknownFileTypes defaults to false, so
// StaticFileMiddleware 404s those files rather than guess a content type. That 404 fails the
// WASM runtime's subresource-integrity check for the file and the whole app fails to boot.
var staticFileTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticFileTypeProvider.Mappings[".dat"] = "application/octet-stream";
staticFileTypeProvider.Mappings[".blat"] = "application/octet-stream";
staticFileTypeProvider.Mappings[".wasm"] = "application/wasm";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileTypeProvider });

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapAuthEndpoints();
app.MapHouseholdEndpoints();
app.MapHub<HouseholdHub>("/hubs/household").RequireAuthorization();

// SPA fallback for the Blazor client - see the UseStaticFiles comment above. Registered
// last so it never shadows an API route; only unmatched GET requests fall through to it.
app.MapFallbackToFile("index.html");

app.Run();
