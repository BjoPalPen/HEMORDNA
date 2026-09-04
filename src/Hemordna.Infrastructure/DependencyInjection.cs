using Hemordna.Application.Households;
using Hemordna.Application.Planning;
using Hemordna.Application.Tasks;
using Hemordna.Infrastructure.Identity;
using Hemordna.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
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
        services.AddScoped<IHouseholdMembershipQuery, HouseholdMembershipQuery>();
        services.AddScoped<IMemberAvailabilityRepository, MemberAvailabilityRepository>();
        services.AddScoped<ITaskDefinitionRepository, TaskDefinitionRepository>();
        services.AddScoped<ITaskOccurrenceRepository, TaskOccurrenceRepository>();
        services.AddScoped<IPlanCandidateQuery, PlanCandidateQuery>();

        // Identity supplies user storage and password hashing. Hemordna never implements its
        // own password handling.
        services.AddIdentityCore<HemordnaUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<HemordnaDbContext>();

        return services;
    }
}
