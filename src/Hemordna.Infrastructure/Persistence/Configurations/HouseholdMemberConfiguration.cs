using Hemordna.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable("HouseholdMembers");

        builder.HasKey(member => member.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(member => member.Id).ValueGeneratedNever();

        builder.Property(member => member.HouseholdId).IsRequired();

        builder.Property(member => member.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(member => member.IsActive).IsRequired();
        builder.Property(member => member.CreatedAt).IsRequired();

        // WeeklyTimeBudget stores its minutes in a private array and exposes no per-weekday
        // properties. Rather than adding seven public properties purely to satisfy the ORM,
        // it maps to a native PostgreSQL integer[] - ordered Sunday..Saturday, matching
        // DayOfWeek's underlying values - which stays queryable via array indexing.
        builder.Property(member => member.WeeklyTimeBudget)
            .HasColumnName("WeeklyTimeBudgetMinutes")
            .HasColumnType("integer[]")
            .IsRequired()
            .HasConversion(
                new ValueConverter<WeeklyTimeBudget, int[]>(
                    budget => ToMinutesPerWeekday(budget),
                    minutes => FromMinutesPerWeekday(minutes)),
                new ValueComparer<WeeklyTimeBudget>(
                    (left, right) => left!.Equals(right),
                    budget => budget.GetHashCode(),
                    budget => budget));

        builder.HasIndex(member => member.HouseholdId);

        // One user signs in as at most one member. The unique index is what stops a second
        // membership from being created behind the application's back.
        builder.HasIndex(member => member.UserId)
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");
    }

    private static int[] ToMinutesPerWeekday(WeeklyTimeBudget budget)
        => Enum.GetValues<DayOfWeek>().Select(budget.MinutesFor).ToArray();

    private static WeeklyTimeBudget FromMinutesPerWeekday(int[] minutes)
        => WeeklyTimeBudget.Create(
            Enum.GetValues<DayOfWeek>().ToDictionary(day => day, day => minutes[(int)day]));
}
