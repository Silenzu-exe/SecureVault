using Microsoft.EntityFrameworkCore;
using SecureVault.Data;
using SecureVault.Models;

namespace SecureVault.Services;

/// <summary>
/// Handles user account creation, authentication, and master password changes.
/// Uses IDbContextFactory to create short-lived DbContext instances per
/// operation, rather than a long-lived Scoped context — important under
/// global Blazor Server interactivity where a Scoped service would
/// otherwise live for the entire circuit/session.
/// </summary>
public class AuthService(
    IDbContextFactory<VaultDbContext> dbContextFactory,
    EncryptionService encryptionService)
{
    public async Task<bool> UserExistsAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.Users.AnyAsync();
    }

    public async Task<User> CreateUserAsync(string masterPassword)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var salt = encryptionService.GenerateSalt();
        var hash = encryptionService.HashMasterPassword(masterPassword, salt);

        var user = new User
        {
            Salt = salt,
            MasterPasswordHash = hash
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    public async Task<User?> AuthenticateAsync(string masterPassword)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var user = await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();
        if (user is null)
            return null;

        var isValid = encryptionService.VerifyMasterPassword(
            masterPassword, user.Salt, user.MasterPasswordHash);

        return isValid ? user : null;
    }

    /// <summary>
    /// Changes the master password and re-encrypts every vault entry with
    /// the newly derived key, in a single transaction.
    /// </summary>
    public async Task ChangeMasterPasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            if (!encryptionService.VerifyMasterPassword(currentPassword, user.Salt, user.MasterPasswordHash))
                throw new InvalidOperationException("Current password is incorrect.");

            var oldKey = encryptionService.DeriveKey(currentPassword, user.Salt);

            var newSalt = encryptionService.GenerateSalt();
            var newKey = encryptionService.DeriveKey(newPassword, newSalt);
            var newHash = encryptionService.HashMasterPassword(newPassword, newSalt);

            var entries = await db.VaultEntries.Where(e => e.UserId == userId).ToListAsync();
            foreach (var entry in entries)
            {
                var decrypted = encryptionService.Decrypt(entry.EncryptedPassword, entry.IV, oldKey);
                var (newCipher, newIv) = encryptionService.Encrypt(decrypted, newKey);
                entry.EncryptedPassword = newCipher;
                entry.IV = newIv;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            user.Salt = newSalt;
            user.MasterPasswordHash = newHash;

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}