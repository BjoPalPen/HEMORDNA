using Hemordna.Domain.Areas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");

        builder.HasKey(area => area.Id);

        builder.Property(area => area.HouseholdId).IsRequired();

        builder.Property(area => area.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(area => area.IsActive).IsRequired();

        builder.HasIndex(area => area.HouseholdId);
    }
}
