using Hemordna.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hemordna.Infrastructure.Persistence.Configurations;

internal sealed class PasskeyCredentialConfiguration : IEntityTypeConfiguration<PasskeyCredential>
{
    public void Configure(EntityTypeBuilder<PasskeyCredential> builder)
    {
        builder.ToTable("PasskeyCredentials");

        // The credential id IS the WebAuthn identifier the authenticator itself generated -
        // there is no separate surrogate key to add.
        builder.HasKey(credential => credential.CredentialId);

        builder.Property(credential => credential.CredentialId).ValueGeneratedNever();

        builder.Property(credential => credential.PublicKey).IsRequired();

        builder.Property(credential => credential.SignCount).IsRequired();

        builder.Property(credential => credential.DeviceLabel)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(credential => credential.CreatedAt).IsRequired();

        builder.HasIndex(credential => credential.UserId);
    }
}
