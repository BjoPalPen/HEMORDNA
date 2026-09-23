using Hemordna.Domain.Households;
using Hemordna.Domain.Push;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");

        builder.HasKey(subscription => subscription.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();

        builder.Property(subscription => subscription.HouseholdId).IsRequired();
        builder.Property(subscription => subscription.MemberId).IsRequired();

        builder.Property(subscription => subscription.Endpoint)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxEndpointLength);

        builder.Property(subscription => subscription.P256dh)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxP256dhLength);

        builder.Property(subscription => subscription.Auth)
            .IsRequired()
            .HasMaxLength(PushSubscription.MaxAuthLength);

        builder.Property(subscription => subscription.CreatedAt).IsRequired();
        builder.Property(subscription => subscription.LastSeenAt).IsRequired();

        // Unique from the start (CLAUDE.md's task instructions): a browser install has exactly
        // one live subscription, so a second POST /subscribe for the same endpoint must update
        // the existing row - see PushSubscription.Reclaim and SubscribeToPush.
        builder.HasIndex(subscription => subscription.Endpoint).IsUnique();

        // The query that actually gets asked is "this member's own devices" - household leads
        // so the index stays tenant-scoped (CLAUDE.md §9).
        builder.HasIndex(subscription => new { subscription.HouseholdId, subscription.MemberId });

        builder.HasOne<HouseholdMember>()
            .WithMany()
            .HasForeignKey(subscription => subscription.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
