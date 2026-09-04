using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class MemberPreferenceConfiguration : IEntityTypeConfiguration<MemberPreference>
{
    public void Configure(EntityTypeBuilder<MemberPreference> builder)
    {
        builder.ToTable("MemberPreferences");

        // One preference row per member - there is nothing else to key it by, and a member
        // has exactly one set of personal preferences.
        builder.HasKey(preference => preference.MemberId);

        builder.Property(preference => preference.HouseholdId).IsRequired();
        builder.Property(preference => preference.Presentation).IsRequired();
        builder.Property(preference => preference.Motivation).IsRequired();

        builder.HasIndex(preference => preference.HouseholdId);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(preference => preference.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
