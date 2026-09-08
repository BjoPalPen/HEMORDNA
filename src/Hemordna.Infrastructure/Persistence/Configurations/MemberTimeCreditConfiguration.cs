using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class MemberTimeCreditConfiguration : IEntityTypeConfiguration<MemberTimeCredit>
{
    public void Configure(EntityTypeBuilder<MemberTimeCredit> builder)
    {
        builder.ToTable("MemberTimeCredits");

        builder.HasKey(entry => entry.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.HouseholdId).IsRequired();
        builder.Property(entry => entry.MemberId).IsRequired();
        builder.Property(entry => entry.OccurredOn).IsRequired();
        builder.Property(entry => entry.Minutes).IsRequired();
        builder.Property(entry => entry.Reason).IsRequired().HasConversion<string>();
        builder.Property(entry => entry.OccurrenceId).IsRequired();

        // The two read patterns the application actually needs: a member's own ledger over a
        // date range (GetMemberTimeCredit, rotation credit), and every row tied to one
        // occurrence (ReopenTaskOccurrence undoing what a completion earned).
        builder.HasIndex(entry => new { entry.HouseholdId, entry.MemberId, entry.OccurredOn });
        builder.HasIndex(entry => entry.OccurrenceId);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(entry => entry.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
