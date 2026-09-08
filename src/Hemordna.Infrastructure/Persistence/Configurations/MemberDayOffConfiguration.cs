using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class MemberDayOffConfiguration : IEntityTypeConfiguration<MemberDayOff>
{
    public void Configure(EntityTypeBuilder<MemberDayOff> builder)
    {
        builder.ToTable("MemberDaysOff");

        // No surrogate Id - a member can only ever have one day-off row per date, so the
        // natural key already uniquely identifies a row and doubles as the lookup this needs
        // most often (IMemberDayOffRepository.FindAsync).
        builder.HasKey(dayOff => new { dayOff.HouseholdId, dayOff.MemberId, dayOff.Date });

        builder.Property(dayOff => dayOff.HouseholdId).IsRequired();
        builder.Property(dayOff => dayOff.MemberId).IsRequired();
        builder.Property(dayOff => dayOff.Date).IsRequired();

        // The household-wide range read (ListForHouseholdAsync, for Vecka/Hushåll's dot-off and
        // for rotation's own exclusion check) filters by household and date but not member.
        builder.HasIndex(dayOff => new { dayOff.HouseholdId, dayOff.Date });

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(dayOff => dayOff.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
