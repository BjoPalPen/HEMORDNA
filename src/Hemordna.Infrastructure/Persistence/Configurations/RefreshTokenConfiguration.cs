using Hemordna.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        // The domain creates its own identifiers; the database never generates one.
        builder.Property(token => token.Id).ValueGeneratedNever();

        // No HasOne/WithMany to the identity user here, matching PasskeyCredential.UserId and
        // HouseholdMember.UserId elsewhere: HemordnaUser lives in ASP.NET Identity's own table
        // set, and none of this codebase's user-linking columns declare a relational foreign
        // key against it - only a plain column plus an index.
        builder.Property(token => token.UserId).IsRequired();

        builder.Property(token => token.ChainId).IsRequired();

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(RefreshToken.TokenHashLength);

        builder.Property(token => token.CreatedAt).IsRequired();
        builder.Property(token => token.ExpiresAt).IsRequired();

        // Null while the token has not been used/invalidated yet - see RefreshToken.ConsumedAt
        // and RefreshToken.RevokedAt.
        builder.Property(token => token.ConsumedAt);
        builder.Property(token => token.RevokedAt);

        // Unique: the incoming query on POST /api/auth/refresh looks a presented token up by its
        // hash and expects at most one match. TokenHash is the SHA-256 digest of a
        // cryptographically random secret (Commit 2), so a collision between two legitimately
        // issued tokens is not a practical concern - a duplicate here would mean the same secret
        // was issued twice, which the unique constraint turns into a hard database error instead
        // of two rows silently answering for the same token.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // "Revoke every token for this user" - password change and logout (see
        // docs/ARCHITECTURE.md "Beslut: Refresh-token med rotation").
        builder.HasIndex(token => token.UserId);

        // "Revoke the whole chain" - reuse detection, the single most important query this
        // table serves. See RefreshToken.ChainId.
        builder.HasIndex(token => token.ChainId);
    }
}
