using Hemordna.Domain.Households;
using Hemordna.Domain.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class ReminderShareConfiguration : IEntityTypeConfiguration<ReminderShare>
{
    public void Configure(EntityTypeBuilder<ReminderShare> builder)
    {
        builder.ToTable("ReminderShares");

        builder.HasKey(share => share.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(share => share.Id).ValueGeneratedNever();

        builder.Property(share => share.ReminderId).IsRequired();
        builder.Property(share => share.MemberId).IsRequired();

        // No HouseholdId, unlike SentReminderNotification. That column exists there only
        // because CLAUDE.md §9 asks every household-reachable row to carry one "for the same
        // reason every household-reachable row does" - but a share is never reached, queried or
        // deleted on its own: it is only ever read through its owning Reminder (already
        // household-scoped by ReminderRepository) or cleaned up by that Reminder's own cascade
        // delete. There is no query this column would serve here, so it is left out on purpose,
        // not forgotten.

        // The relationship to Reminder (ReminderId, cascading) is configured from the Reminder
        // side in ReminderConfiguration, alongside the Shares navigation it backs - see the
        // comment there. Only the relationship to HouseholdMember is configured here, since
        // HouseholdMember exposes no reverse navigation to shares.
        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(share => share.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        // No explicit index beyond what EF already creates by convention for each FK
        // (IX_ReminderShares_ReminderId, IX_ReminderShares_MemberId) - the upcoming query reads
        // "does this member have a share row for this reminder", i.e. ReminderId first, then
        // MemberId, and the FK index on ReminderId alone already narrows that to the handful of
        // rows one reminder can have (at most household size - 1, the same small-table
        // reasoning as the comment on ReminderConfiguration's own index and
        // ReminderRepository.DeleteOlderThanAsync). A composite (ReminderId, MemberId) index
        // would only pay for itself if this table grew unbounded, which it structurally cannot.
    }
}
