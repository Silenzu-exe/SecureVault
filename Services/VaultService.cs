using Microsoft.EntityFrameworkCore;
using SecureVault.Data;
using SecureVault.Models;

namespace SecureVault.Services;

/// <summary>
/// CRUD operations for vault entries. Passwords are encrypted before
/// storage and decrypted only on demand (never held decrypted in bulk).
/// </summary>
public class VaultService(
    IDbContextFactory<VaultDbContext> dbContextFactory,
    EncryptionService encryptionService)
{
    public async Task<List<VaultEntry>> GetEntriesAsync(Guid userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.VaultEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync();
    }

    public async Task<VaultEntry> AddEntryAsync(
        Guid userId,
        string siteName,
        string username,
        string password,
        string? notes,
        byte[] encryptionKey)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var (cipherText, iv) = encryptionService.Encrypt(password, encryptionKey);

        var entry = new VaultEntry
        {
            UserId = userId,
            SiteName = siteName,
            Username = username,
            EncryptedPassword = cipherText,
            IV = iv,
            Notes = notes
        };

        db.VaultEntries.Add(entry);
        await db.SaveChangesAsync();

        return entry;
    }

    public async Task UpdateEntryAsync(
        Guid entryId,
        string siteName,
        string username,
        string? newPassword,
        string? notes,
        byte[] encryptionKey)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var entry = await db.VaultEntries.FirstOrDefaultAsync(e => e.Id == entryId)
            ?? throw new InvalidOperationException("Entry not found.");

        entry.SiteName = siteName;
        entry.Username = username;
        entry.Notes = notes;
        entry.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(newPassword))
        {
            var (cipherText, iv) = encryptionService.Encrypt(newPassword, encryptionKey);
            entry.EncryptedPassword = cipherText;
            entry.IV = iv;
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(Guid entryId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var entry = await db.VaultEntries.FirstOrDefaultAsync(e => e.Id == entryId);
        if (entry is not null)
        {
            db.VaultEntries.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    public string DecryptPassword(VaultEntry entry, byte[] encryptionKey)
    {
        return encryptionService.Decrypt(entry.EncryptedPassword, entry.IV, encryptionKey);
    }
}