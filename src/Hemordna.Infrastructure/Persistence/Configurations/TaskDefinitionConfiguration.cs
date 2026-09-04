using System.Text.Json;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Households;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class TaskDefinitionConfiguration : IEntityTypeConfiguration<TaskDefinition>
{
    public void Configure(EntityTypeBuilder<TaskDefinition> builder)
    {
        builder.ToTable("TaskDefinitions");

        builder.HasKey(definition => definition.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(definition => definition.Id).ValueGeneratedNever();

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

        // RecurrenceRule exposes only its existing public API - no ORM-only properties added
        // for the ORM's sake - so it round-trips as a small JSON document through the same
        // public factories a caller would use (see HouseholdMemberConfiguration for the same
        // reasoning applied to WeeklyTimeBudget).
        builder.Property(definition => definition.Recurrence)
            .HasColumnName("Recurrence")
            .HasConversion(
                new ValueConverter<RecurrenceRule?, string?>(
                    rule => rule == null ? null : JsonSerializer.Serialize(ToDto(rule), JsonOptions),
                    json => json == null ? null : FromDto(JsonSerializer.Deserialize<RecurrenceRuleDto>(json, JsonOptions)!)),
                new ValueComparer<RecurrenceRule?>(
                    (left, right) => Equals(left, right),
                    rule => rule == null ? 0 : rule.GetHashCode(),
                    rule => rule));

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

    private static readonly JsonSerializerOptions JsonOptions = new();

    private sealed record RecurrenceRuleDto(
        RecurrenceFrequency Frequency, int Interval, DateOnly StartDate, DayOfWeek? Weekday, WeekOfMonth? MonthlyWeek);

    private static RecurrenceRuleDto ToDto(RecurrenceRule rule)
        => new(rule.Frequency, rule.Interval, rule.StartDate, rule.Weekday, rule.MonthlyWeek);

    private static RecurrenceRule FromDto(RecurrenceRuleDto dto) => dto switch
    {
        { MonthlyWeek: { } which } => RecurrenceRule.MonthlyOnWeekday(dto.StartDate, which, dto.Weekday!.Value, dto.Interval),
        { Frequency: RecurrenceFrequency.Weekly } => RecurrenceRule.Weekly(dto.StartDate, dto.Weekday!.Value, dto.Interval),
        { Frequency: RecurrenceFrequency.Monthly } => RecurrenceRule.Monthly(dto.StartDate, dto.Interval),
        _ => RecurrenceRule.Daily(dto.StartDate, dto.Interval)
    };
}
