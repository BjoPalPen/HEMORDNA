using Hemordna.Api.Endpoints;
using Hemordna.Application.Households;
using Hemordna.Infrastructure;
using Hemordna.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);

// TimeProvider rather than a static clock, so use cases stay testable.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateHousehold>();
builder.Services.AddScoped<GetHousehold>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<HemordnaDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapHouseholdEndpoints();

app.Run();
