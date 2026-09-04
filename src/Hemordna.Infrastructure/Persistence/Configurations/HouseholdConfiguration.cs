using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Households");

        builder.HasKey(household => household.Id);

        builder.Property(household => household.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(household => household.CreatedAt).IsRequired();

        // Members and areas are exposed as read-only collections, so EF reads and writes the
        // backing fields directly instead of going through the public surface.
        builder.HasMany(household => household.Members)
            .WithOne()
            .HasForeignKey(member => member.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Household.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(household => household.Areas)
            .WithOne()
            .HasForeignKey(area => area.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Household.Areas))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
