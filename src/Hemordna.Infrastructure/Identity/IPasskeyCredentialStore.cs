namespace Hemordna.Infrastructure.Identity;

public interface IPasskeyCredentialStore
{
    Task<IReadOnlyList<PasskeyCredential>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<PasskeyCredential?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(byte[] credentialId, CancellationToken cancellationToken);

    Task AddAsync(PasskeyCredential credential, CancellationToken cancellationToken);

    Task UpdateSignCountAsync(byte[] credentialId, uint signCount, CancellationToken cancellationToken);

    /// <summary>Removes a credential, scoped to its owner so one person can never remove
    /// another's passkey by guessing or replaying an id.</summary>
    Task<bool> RemoveAsync(byte[] credentialId, Guid userId, CancellationToken cancellationToken);
}
