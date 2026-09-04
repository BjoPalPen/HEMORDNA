using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class TaskDefinitionConfiguration : IEntityTypeConfiguration<TaskDefinition>
{
    public void Configure(EntityTypeBuilder<TaskDefinition> builder)
    {
        builder.ToTable("TaskDefinitions");

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.HouseholdId).IsRequired();

        builder.Property(definition => definition.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(definition => definition.Description).HasMaxLength(2000);

        builder.Property(definition => definition.EstimatedMinutes).IsRequired();

        // Stored as its underlying int: the numeric order of TaskPriority is part of the
        // domain contract, so ordering survives into the database.
        builder.Property(definition => definition.Priority).IsRequired();

        builder.Property(definition => definition.CanBeDeferred).IsRequired();
        builder.Property(definition => definition.HasRotatingResponsibility).IsRequired();
        builder.Property(definition => definition.RequiresMultiplePeople).IsRequired();
        builder.Property(definition => definition.IsActive).IsRequired();
        builder.Property(definition => definition.CreatedAt).IsRequired();

        builder.HasIndex(definition => definition.HouseholdId);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(definition => definition.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // An area can be removed without destroying the task definitions that referenced it.
        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(definition => definition.AreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(definition => definition.DefaultResponsibleMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
