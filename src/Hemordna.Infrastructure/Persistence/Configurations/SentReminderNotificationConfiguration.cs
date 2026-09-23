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
        builder.Property(entry => entry.SentAt).IsRequired();

        // The idempotency guarantee itself (docs for SentReminderNotification): a given reminder
        // notification of a given kind can be recorded at most once.
        builder.HasIndex(entry => new { entry.ReminderId, entry.Kind }).IsUnique();

        builder.HasOne<Reminder>()
            .WithMany()
            .HasForeignKey(entry => entry.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
