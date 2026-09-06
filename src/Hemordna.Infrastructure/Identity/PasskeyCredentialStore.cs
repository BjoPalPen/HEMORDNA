using Hemordna.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Identity;

internal sealed class PasskeyCredentialStore : IPasskeyCredentialStore
{
    private readonly HemordnaDbContext _dbContext;

    public PasskeyCredentialStore(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<PasskeyCredential>> ListByUserIdAsync(
        Guid userId, CancellationToken cancellationToken)
        => await _dbContext.PasskeyCredentials
            .Where(credential => credential.UserId == userId)
            .OrderBy(credential => credential.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<PasskeyCredential?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken)
        => _dbContext.PasskeyCredentials
            .FirstOrDefaultAsync(credential => credential.CredentialId == credentialId, cancellationToken);

    public Task<bool> ExistsAsync(byte[] credentialId, CancellationToken cancellationToken)
        => _dbContext.PasskeyCredentials
            .AnyAsync(credential => credential.CredentialId == credentialId, cancellationToken);

    public async Task AddAsync(PasskeyCredential credential, CancellationToken cancellationToken)
    {
        await _dbContext.PasskeyCredentials.AddAsync(credential, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSignCountAsync(byte[] credentialId, uint signCount, CancellationToken cancellationToken)
    {
        var credential = await _dbContext.PasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);

        if (credential is not null)
        {
            credential.SignCount = signCount;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> RemoveAsync(byte[] credentialId, Guid userId, CancellationToken cancellationToken)
    {
        var credential = await _dbContext.PasskeyCredentials
            .FirstOrDefaultAsync(
                c => c.CredentialId == credentialId && c.UserId == userId, cancellationToken);

        if (credential is null)
        {
            return false;
        }

        _dbContext.PasskeyCredentials.Remove(credential);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
