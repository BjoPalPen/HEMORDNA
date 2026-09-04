using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");

        builder.HasKey(assignment => assignment.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        builder.Property(assignment => assignment.HouseholdId).IsRequired();
        builder.Property(assignment => assignment.TaskDefinitionId).IsRequired();
        builder.Property(assignment => assignment.MemberId).IsRequired();
        builder.Property(assignment => assignment.ScheduledDate).IsRequired();
        builder.Property(assignment => assignment.CreatedAt).IsRequired();

        // Rotation always asks for "the most recent assignment for this definition" - the
        // natural query this history exists to serve.
        builder.HasIndex(assignment => new { assignment.TaskDefinitionId, assignment.ScheduledDate });

        builder.HasIndex(assignment => assignment.HouseholdId);

        builder.HasOne<TaskDefinition>()
            .WithMany()
            .HasForeignKey(assignment => assignment.TaskDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(assignment => assignment.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
