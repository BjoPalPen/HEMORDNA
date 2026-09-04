using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class MemberAvailabilityConfiguration : IEntityTypeConfiguration<MemberAvailability>
{
    public void Configure(EntityTypeBuilder<MemberAvailability> builder)
    {
        builder.ToTable("MemberAvailabilities");

        builder.HasKey(availability => availability.Id);

        builder.Property(availability => availability.HouseholdId).IsRequired();
        builder.Property(availability => availability.MemberId).IsRequired();
        builder.Property(availability => availability.Date).IsRequired();
        builder.Property(availability => availability.AvailableMinutes).IsRequired();

        // An override is per member and date - a second one for the same day would make
        // "how much time does this person have today" ambiguous.
        builder.HasIndex(availability => new { availability.MemberId, availability.Date })
            .IsUnique();

        builder.HasIndex(availability => availability.HouseholdId);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(availability => availability.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
