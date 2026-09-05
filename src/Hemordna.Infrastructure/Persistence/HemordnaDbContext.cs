using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Hemordna.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

/// <summary>
/// The application's PostgreSQL context. All mapping lives in
/// <see cref="Configurations"/> via Fluent API - the domain carries no EF attributes.
/// </summary>
/// <remarks>
/// Identity lives in this same context rather than a second one, so the database has a
/// single migration history and a user and their first household member can be written in
/// one transaction.
/// </remarks>
public sealed class HemordnaDbContext : IdentityDbContext<HemordnaUser, IdentityRole<Guid>, Guid>
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

    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();

    public DbSet<MemberPreference> MemberPreferences => Set<MemberPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HemordnaDbContext).Assembly);
    }
}
