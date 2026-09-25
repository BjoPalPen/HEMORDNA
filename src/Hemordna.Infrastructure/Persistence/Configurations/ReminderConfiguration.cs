using Hemordna.Domain.Households;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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

        // Null when no travel time is tracked - see Reminder.TravelMinutes and MoveTo/SetTravelMinutes.
        builder.Property(reminder => reminder.TravelMinutes);

        builder.Property(reminder => reminder.Status).IsRequired();
        builder.Property(reminder => reminder.CreatedAt).IsRequired();
        builder.Property(reminder => reminder.Visibility).IsRequired();

        // Every existing reminder must keep exactly its current visibility when this column is
        // introduced - the migration backfills it to Everyone (0), which is what "Household"-
        // visibility reminders already behaved as before Audience existed (see
        // docs/ARCHITECTURE.md, "Beslut: Synlighet för påminnelser"). Changing that default
        // would silently narrow or widen who sees an already-scheduled time.
        builder.Property(reminder => reminder.Audience).IsRequired();

        // The query that actually gets asked is "this member's reminders on this date" -
        // household leads so the index stays tenant-scoped (CLAUDE.md §9).
        builder.HasIndex(reminder => new
        {
            reminder.HouseholdId,
            reminder.MemberId,
            reminder.Date
        });

        // No separate index for the upcoming "other members' visible reminders" query
        // (HouseholdId, Date, Visibility - see IReminderRepository.ListVisibleForOthersInRangeAsync,
        // added in a later commit). It is a different filter shape than the index above, but
        // reminders per household are few - a handful of appointments, not a growing work log
        // like TaskOccurrence - so a sequential scan already narrowed by HouseholdId is cheap.
        // Add an index only if this ever shows up as a real cost, not speculatively.

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(reminder => reminder.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Shares is exposed as a read-only collection (see Reminder.Shares), so EF reads and
        // writes the backing field directly instead of going through the public surface - the
        // same approach as Household.Members/Areas in HouseholdConfiguration. Deleting a
        // reminder deletes its shares with it; there is nothing left to share once the
        // reminder itself is gone.
        builder.HasMany(reminder => reminder.Shares)
            .WithOne()
            .HasForeignKey(share => share.ReminderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Reminder.Shares))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
