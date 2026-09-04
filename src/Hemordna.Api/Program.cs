using System.Text;
using System.Text.Json.Serialization;
using Hemordna.Api;
using Hemordna.Api.Authentication;
using Hemordna.Api.Endpoints;
using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tasks;
using Hemordna.Infrastructure;
using Hemordna.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddScoped<GetHousehold>();
builder.Services.AddScoped<AddHouseholdMember>();
builder.Services.AddScoped<AddArea>();
builder.Services.AddScoped<SetMemberAvailability>();
builder.Services.AddScoped<SetMemberWeeklyBudget>();
builder.Services.AddScoped<CreateTaskDefinition>();
builder.Services.AddScoped<ScheduleTaskOccurrence>();
builder.Services.AddScoped<CompleteTaskOccurrence>();
builder.Services.AddScoped<DeferTaskOccurrence>();
builder.Services.AddScoped<GetDailyPlan>();
builder.Services.AddSingleton<DailyPlanner>();

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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (allowedOrigins.Length > 0)
{
    app.UseCors();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

app.MapAuthEndpoints();
app.MapHouseholdEndpoints();

app.Run();
