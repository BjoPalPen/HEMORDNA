using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

/// <summary>
/// The application's PostgreSQL context. All mapping lives in
/// <see cref="Configurations"/> via Fluent API - the domain carries no EF attributes.
/// </summary>
public sealed class HemordnaDbContext : DbContext
{
    public HemordnaDbContext(DbContextOptions<HemordnaDbContext> options) : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    public DbSet<MemberAvailability> MemberAvailabilities => Set<MemberAvailability>();

    public DbSet<Area> Areas => Set<Area>();

    public DbSet<TaskDefinition> TaskDefinitions => Set<TaskDefinition>();

    public DbSet<TaskOccurrence> TaskOccurrences => Set<TaskOccurrence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(HemordnaDbContext).Assembly);
}
