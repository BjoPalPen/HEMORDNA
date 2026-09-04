using Hemordna.Application.Households;
using Hemordna.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hemordna.Infrastructure;

/// <summary>
/// Registers the infrastructure layer. The API composes the application through this and
/// stays unaware of EF Core, Npgsql and the persistence implementations.
/// </summary>
public static class DependencyInjection
{
    /// <summary>The configuration key holding the PostgreSQL connection string.</summary>
    public const string ConnectionStringName = "Hemordna";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Failing loudly at startup beats a running API that cannot reach its database.
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set " +
                $"ConnectionStrings__{ConnectionStringName} in the environment or in configuration.");
        }

        services.AddDbContext<HemordnaDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IHouseholdRepository, HouseholdRepository>();

        return services;
    }
}
