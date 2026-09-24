using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class SentReminderNotificationConfiguration : IEntityTypeConfiguration<SentReminderNotification>
{
    public void Configure(EntityTypeBuilder<SentReminderNotification> builder)
    {
        builder.ToTable("SentReminderNotifications");

        builder.HasKey(entry => entry.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.HouseholdId).IsRequired();
        builder.Property(entry => entry.ReminderId).IsRequired();
        builder.Property(entry => entry.Kind).IsRequired();
        builder.Property(entry => entry.ScheduledFor).IsRequired();
        builder.Property(entry => entry.SentAt).IsRequired();

        // The idempotency guarantee itself (docs for SentReminderNotification): a given reminder
        // notification of a given kind, due at a given instant, can be recorded at most once. A
        // reminder moved (or re-timed) after its notification already fired gets a new
        // ScheduledFor, which is therefore a new key - a fresh row, not an upsert, and the old
        // row is left behind for the existing reminder cleanup to remove later.
        builder.HasIndex(entry => new { entry.ReminderId, entry.Kind, entry.ScheduledFor }).IsUnique();

        builder.HasOne<Reminder>()
            .WithMany()
            .HasForeignKey(entry => entry.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
