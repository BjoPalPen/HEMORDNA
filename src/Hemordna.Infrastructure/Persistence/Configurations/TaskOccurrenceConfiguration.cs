using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class TaskOccurrenceConfiguration : IEntityTypeConfiguration<TaskOccurrence>
{
    public void Configure(EntityTypeBuilder<TaskOccurrence> builder)
    {
        builder.ToTable("TaskOccurrences");

        builder.HasKey(occurrence => occurrence.Id);

        builder.Property(occurrence => occurrence.HouseholdId).IsRequired();
        builder.Property(occurrence => occurrence.TaskDefinitionId).IsRequired();
        builder.Property(occurrence => occurrence.ScheduledDate).IsRequired();
        builder.Property(occurrence => occurrence.OriginalScheduledDate).IsRequired();

        // Snapshots taken from the definition when the occurrence was scheduled.
        builder.Property(occurrence => occurrence.EstimatedMinutes).IsRequired();
        builder.Property(occurrence => occurrence.Priority).IsRequired();
        builder.Property(occurrence => occurrence.CanBeDeferred).IsRequired();

        builder.Property(occurrence => occurrence.Status).IsRequired();
        builder.Property(occurrence => occurrence.CreatedAt).IsRequired();

        // The planner asks "what is outstanding for this household on or before this date",
        // so the household is the leading column of the index as well as the tenant key.
        builder.HasIndex(occurrence => new
        {
            occurrence.HouseholdId,
            occurrence.ScheduledDate,
            occurrence.Status
        });

        builder.HasIndex(occurrence => occurrence.AssignedMemberId);

        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(occurrence => occurrence.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deactivating or removing a member must not delete the history of what was done.
        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(occurrence => occurrence.AssignedMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
