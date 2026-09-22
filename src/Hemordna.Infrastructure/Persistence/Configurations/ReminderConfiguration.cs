using Hemordna.Domain.Households;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("Reminders");

        builder.HasKey(reminder => reminder.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(reminder => reminder.Id).ValueGeneratedNever();

        builder.Property(reminder => reminder.HouseholdId).IsRequired();
        builder.Property(reminder => reminder.MemberId).IsRequired();

        builder.Property(reminder => reminder.Title)
            .IsRequired()
            .HasMaxLength(Reminder.MaxTitleLength);

        builder.Property(reminder => reminder.Location)
            .HasMaxLength(Reminder.MaxLocationLength);

        builder.Property(reminder => reminder.Date).IsRequired();

        // "All day" is a real state (null), not a missing value - no IsRequired().
        builder.Property(reminder => reminder.TimeOfDay);

        builder.Property(reminder => reminder.Status).IsRequired();
        builder.Property(reminder => reminder.CreatedAt).IsRequired();

        // The query that actually gets asked is "this member's reminders on this date" -
        // household leads so the index stays tenant-scoped (CLAUDE.md §9).
        builder.HasIndex(reminder => new
        {
            reminder.HouseholdId,
            reminder.MemberId,
            reminder.Date
        });

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(reminder => reminder.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
