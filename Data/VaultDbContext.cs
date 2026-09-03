using Microsoft.EntityFrameworkCore;
using SecureVault.Models;

namespace SecureVault.Data;

public class VaultDbContext(DbContextOptions<VaultDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<VaultEntry> VaultEntries => Set<VaultEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VaultEntry>()
            .HasIndex(v => v.UserId);

        modelBuilder.Entity<VaultEntry>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}