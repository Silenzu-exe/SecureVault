# SecureVault — Rebuild Handoff Notes

> Give this file to a new Claude conversation (upload it or paste it in) to
> resume exactly where we left off. Say something like "continue from this
> handoff doc, we're about to do Stage 5."

## Project location & basics

- **Path**: `/home/silenzu/RiderProjects/SecureVault`
- **Namespace**: `SecureVault`
- **Target**: Blazor Web App, **.NET 10** (only SDK available on this
  machine; net8.0 install attempt failed on a DNS issue reaching Microsoft's
  installer CDN — NuGet itself works fine, unrelated problem, not worth
  chasing further)
- **Interactivity**: **Global** (`--all-interactive`) — this is deliberate
  and important, see "How the app works" below.
- **Run**: `dotnet run` from the project folder → `http://localhost:5066`

This is a REBUILD of an earlier AI-generated project that had a long chain of
structural Blazor bugs (mixed old/new template patterns, session state loss,
circuit crashes). That old project lives at
`/home/silenzu/RiderProjects/Secure_Vault` (note the underscore — different
folder, different namespace `Secure_Vault`) and is being used only as a
reference for business-logic code, not architecture. Full history of that
project's bugs is in the "Old project bug history" section at the bottom —
**do not re-diagnose those, they don't apply to this new build.**

## How this app works (architecture overview)

**What it is**: A password manager. User sets one "master password" on
first run. Every stored credential is AES-256 encrypted using a key derived
from that master password via PBKDF2. The master password itself is never
stored — only a PBKDF2 hash of it, for verification at login.

**Why global interactivity matters here**: Blazor Server keeps a live
WebSocket ("circuit") between browser and server, and login state lives in
a `Scoped` `SessionService` tied to that circuit. If pages declare interactive
render mode individually (the "per-page" pattern), Blazor can spin up a
**new circuit per navigation**, silently wiping that scoped session — this
is exactly what broke the old project (user would log in successfully, then
get bounced back to the login page navigating to `/vault`). Using **global**
interactivity (set once, app-wide) keeps one circuit alive for the whole
session, so login state persists correctly as the user moves between pages.

**Why `IDbContextFactory` instead of a normal injected `DbContext`**: EF
Core's `DbContext` is not safe for concurrent use, and a normal `Scoped`
registration would tie one `DbContext` instance to the *entire circuit*
under global interactivity — i.e., the whole user session, not just one
request. Any overlapping database calls on that single instance risk a
`"A second operation was started on this context instance..."` exception.
Instead, every service that touches the database (`AuthService`,
`VaultService`) takes an `IDbContextFactory<VaultDbContext>` and creates a
short-lived context per method call (`await using var db = await
dbContextFactory.CreateDbContextAsync();`). This is the officially
recommended pattern for Blazor Server + EF Core.

**Encryption flow**:
1. User sets master password → `EncryptionService.GenerateSalt()` makes a
   random salt → `HashMasterPassword()` (PBKDF2, 100k iterations) → hash +
   salt stored in `Users` table. Master password itself is discarded from
   the DB layer immediately.
2. On login, same salt + entered password → re-derive hash →
   `CryptographicOperations.FixedTimeEquals` constant-time comparison
   against stored hash.
3. On successful login, `EncryptionService.DeriveKey(password, salt)`
   produces a 256-bit AES key, held ONLY in memory in `SessionService`
   (`byte[] EncryptionKey`) for the duration of the session — never
   persisted, cleared on logout/lock via `Array.Clear`.
4. Each vault entry's password is encrypted individually with
   `EncryptionService.Encrypt()` (AES-256-CBC, fresh random IV per entry,
   both ciphertext and IV stored Base64-encoded per row). Decrypted only
   on-demand when the user clicks "reveal" — never bulk-decrypted.

**Master password change**: `AuthService.ChangeMasterPasswordAsync()` runs
inside an EF Core transaction — decrypts every vault entry with the old
derived key, re-encrypts with a newly derived key, updates the user's
salt/hash, commits atomically (rolls back entirely on any failure).

## Stage tracker

- [x] **Stage 0 — Scaffold.** `dotnet new blazor -o SecureVault
      --interactivity Server --all-interactive`. Had to add
      `<AllowMissingPrunePackageData>true</AllowMissingPrunePackageData>` to
      `.csproj` to work around a `NETSDK1226` restore error (.NET 10 SDK
      package-pruning metadata fetch issue). EF Core packages installed:
      `Microsoft.EntityFrameworkCore.Sqlite`, `.Design`, `.Tools`, all
      `10.0.11`. Confirmed `dotnet run` shows default template pages
      (Home/Counter/Weather) successfully — genuinely verified, not just
      assumed.
- [x] **Stage 1 — Models.** `Models/User.cs`, `Models/VaultEntry.cs`.
      Plain data classes, `Guid` ids, no logic. Confirmed builds clean.
- [x] **Stage 2 — Data layer.** `Data/VaultDbContext.cs`. `DbSet<User>`,
      `DbSet<VaultEntry>`, FK + index configured in `OnModelCreating`.
      Confirmed builds clean. NOT YET wired into DI (that's Stage 5, via
      `IDbContextFactory`, not plain `AddDbContext`).
- [x] **Stage 3 — Security & Auth.** `Services/EncryptionService.cs`
      (AES-256-CBC encrypt/decrypt, PBKDF2 derive/hash, constant-time
      verify, all stateless) and `Services/AuthService.cs`
      (`UserExistsAsync`, `CreateUserAsync`, `AuthenticateAsync`,
      `ChangeMasterPasswordAsync` — written from the start using the
      `IDbContextFactory<VaultDbContext>` constructor-injection pattern).
      Confirmed builds clean.
- [x] **Stage 4 — Vault, Generator, Session services.**
      `Services/VaultService.cs` (CRUD, on-demand decrypt, also uses
      `IDbContextFactory`), `Services/PasswordGeneratorService.cs`
      (crypto-secure `RandomNumberGenerator`-based generation, Shannon
      entropy calc, common-pattern weak-password detection, strength
      rating enum `VeryWeak`→`Excellent`), `Services/SessionService.cs`
      (plain Scoped service — no DbContext needed — holds `CurrentUser`,
      in-memory `EncryptionKey`, `IsAuthenticated`/`IsLocked`,
      `Login`/`Logout`/`Lock`/`Unlock`/`RecordActivity`,
      `OnSessionChanged` event for UI to subscribe to). Confirmed builds
      clean.
- [ ] **Stage 5 — `Program.cs` wiring.** NEXT STEP. Needs:
      - `IDbContextFactory<VaultDbContext>` registration (pooled factory,
        pointing at a SQLite connection string, e.g. `Data Source=app.db`)
      - `AuthService`, `VaultService`, `PasswordGeneratorService` as Scoped
        or Transient (they're stateless aside from the injected factory, so
        either works — lean Scoped for consistency)
      - `SessionService` as **Scoped** specifically (must persist per
        circuit/session)
      - `AddRazorComponents().AddInteractiveServerComponents(options =>
        options.DetailedErrors = true)` in Development — this was a real
        lesson from the old project: without `DetailedErrors`, circuit
        crashes just show a generic "Rejoining the server..." with no
        useful information in the browser.
      - Apply migrations on startup (`db.Database.Migrate()` in a startup
        scope, or `EnsureCreated()` for early dev — decide which; old
        project used explicit `Migrate()` inside a scope at startup, worked
        fine, can be reused).
- [ ] **Stage 6 — `App.razor`, `Routes.razor`, `MainLayout.razor`.** Plan:
      **use the scaffold's own generated versions, don't hand-edit the
      document shell.** This is the single most important lesson from the
      old project — hand-editing `App.razor`'s HTML shell caused a malformed
      `</head` tag, a missing `<base href="/" />`, and a duplicated nested
      `<html>` document in `MainLayout.razor`, all of which broke Blazor's
      circuit/interactivity in hard-to-diagnose ways. Just add nav
      links/branding to `MainLayout.razor`'s existing structure, nothing
      structural.
- [ ] **Stage 7 — Pages.** Index (login), Setup (first-run account
      creation), Vault (CRUD table + modals), Generator (password generator
      UI), Settings (change master password, auto-lock timeout), Unlock
      (re-auth after inactivity lock). User wants a **professional-looking
      UI**, not default Bootstrap template look — old project's UI was
      plain Bootstrap cards, should be visibly upgraded this time (custom
      CSS/design system, not just Bootstrap defaults). Delete template demo
      pages (Home/Counter/Weather) as part of this stage.
- [ ] **Stage 8 — Migrations.** `dotnet ef migrations add InitialCreate`,
      confirm `app.db` creates with correct schema.
- [ ] **Stage 9 (not yet planned in detail)** — polish pass: confirm
      auto-lock actually works end-to-end, confirm master password change
      re-encrypts correctly, general QA pass against
      `SecureVault-Requirements.md` (original assignment spec — re-upload if
      the new conversation needs it).

## All code written so far (copy-paste ready)

### `Models/User.cs`
```csharp
namespace SecureVault.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MasterPasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### `Models/VaultEntry.cs`
```csharp
namespace SecureVault.Models;

public class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string IV { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### `Data/VaultDbContext.cs`
```csharp
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
```

### `Services/EncryptionService.cs`
```csharp
using System.Security.Cryptography;

namespace SecureVault.Services;

public class EncryptionService
{
    private const int SaltSizeBytes = 16;
    private const int IvSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Pbkdf2Iterations = 100_000;

    public string GenerateSalt()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        return Convert.ToBase64String(salt);
    }

    private string GenerateIV()
    {
        var iv = RandomNumberGenerator.GetBytes(IvSizeBytes);
        return Convert.ToBase64String(iv);
    }

    public byte[] DeriveKey(string password, string base64Salt)
    {
        var salt = Convert.FromBase64String(base64Salt);
        return Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
    }

    public string HashMasterPassword(string password, string base64Salt)
    {
        var hash = DeriveKey(password, base64Salt);
        return Convert.ToBase64String(hash);
    }

    public bool VerifyMasterPassword(string password, string base64Salt, string storedHash)
    {
        var computedHash = HashMasterPassword(password, base64Salt);
        var computedBytes = Convert.FromBase64String(computedHash);
        var storedBytes = Convert.FromBase64String(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    public (string CipherText, string IV) Encrypt(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return (Convert.ToBase64String(cipherBytes), Convert.ToBase64String(aes.IV));
    }

    public string Decrypt(string cipherTextBase64, string ivBase64, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = Convert.FromBase64String(ivBase64);

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherTextBase64);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
```

### `Services/AuthService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using SecureVault.Data;
using SecureVault.Models;

namespace SecureVault.Services;

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

        var user = new User { Salt = salt, MasterPasswordHash = hash };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    public async Task<User?> AuthenticateAsync(string masterPassword)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        var user = await db.Users.OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();
        if (user is null) return null;

        var isValid = encryptionService.VerifyMasterPassword(
            masterPassword, user.Salt, user.MasterPasswordHash);

        return isValid ? user : null;
    }

    public async Task ChangeMasterPasswordAsync(
        Guid userId, string currentPassword, string newPassword)
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
```

### `Services/VaultService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using SecureVault.Data;
using SecureVault.Models;

namespace SecureVault.Services;

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
        Guid userId, string siteName, string username, string password,
        string? notes, byte[] encryptionKey)
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
        Guid entryId, string siteName, string username, string? newPassword,
        string? notes, byte[] encryptionKey)
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
```

### `Services/PasswordGeneratorService.cs`
```csharp
using System.Security.Cryptography;
using System.Text;

namespace SecureVault.Services;

public enum StrengthLevel { VeryWeak, Weak, Fair, Good, Strong, Excellent }

public class PasswordStrength
{
    public StrengthLevel Level { get; set; }
    public int Score { get; set; }
    public double EntropyBits { get; set; }
}

public class PasswordGeneratorService
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}<>?";

    public string GeneratePassword(
        int length, bool useLowercase = true, bool useUppercase = true,
        bool useDigits = true, bool useSymbols = true)
    {
        var pool = new StringBuilder();
        if (useLowercase) pool.Append(Lowercase);
        if (useUppercase) pool.Append(Uppercase);
        if (useDigits) pool.Append(Digits);
        if (useSymbols) pool.Append(Symbols);

        if (pool.Length == 0)
            throw new ArgumentException("At least one character set must be selected.");

        var result = new StringBuilder(length);
        var poolString = pool.ToString();

        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(poolString.Length);
            result.Append(poolString[index]);
        }

        return result.ToString();
    }

    public double CalculateEntropy(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;

        var poolSize = 0;
        if (password.Any(char.IsLower)) poolSize += 26;
        if (password.Any(char.IsUpper)) poolSize += 26;
        if (password.Any(char.IsDigit)) poolSize += 10;
        if (password.Any(c => Symbols.Contains(c))) poolSize += Symbols.Length;

        if (poolSize == 0) poolSize = 26;

        return password.Length * Math.Log2(poolSize);
    }

    public bool IsCommonPattern(string password)
    {
        var lower = password.ToLowerInvariant();
        string[] commonPatterns =
        [
            "123456", "password", "qwerty", "abc123", "letmein",
            "welcome", "admin", "111111", "12345678"
        ];
        return commonPatterns.Any(lower.Contains);
    }

    public PasswordStrength RatePasswordStrength(string password)
    {
        if (string.IsNullOrEmpty(password))
            return new PasswordStrength { Level = StrengthLevel.VeryWeak, Score = 0, EntropyBits = 0 };

        var entropy = CalculateEntropy(password);
        var isCommon = IsCommonPattern(password);

        var (level, score) = entropy switch
        {
            _ when isCommon => (StrengthLevel.VeryWeak, 10),
            < 28 => (StrengthLevel.Weak, 25),
            < 36 => (StrengthLevel.Fair, 45),
            < 60 => (StrengthLevel.Good, 65),
            < 80 => (StrengthLevel.Strong, 85),
            _ => (StrengthLevel.Excellent, 100)
        };

        return new PasswordStrength { Level = level, Score = score, EntropyBits = entropy };
    }
}
```

### `Services/SessionService.cs`
```csharp
using SecureVault.Models;

namespace SecureVault.Services;

public class SessionService
{
    private User? _currentUser;
    private byte[]? _encryptionKey;
    private DateTime _lastActivityTime;
    private int _inactivityTimeoutMinutes = 5;

    public event Action? OnSessionChanged;

    public User? CurrentUser => _currentUser;
    public byte[]? EncryptionKey => _encryptionKey;

    public bool IsAuthenticated => _currentUser != null && _encryptionKey != null && !IsLocked;

    public bool IsLocked =>
        _currentUser != null &&
        DateTime.UtcNow.Subtract(_lastActivityTime).TotalMinutes > _inactivityTimeoutMinutes;

    public int InactivityTimeoutMinutes
    {
        get => _inactivityTimeoutMinutes;
        set => _inactivityTimeoutMinutes = value > 0 ? value : 5;
    }

    public void Login(User user, byte[] encryptionKey)
    {
        _currentUser = user;
        _encryptionKey = encryptionKey;
        _lastActivityTime = DateTime.UtcNow;
        OnSessionChanged?.Invoke();
    }

    public void Logout()
    {
        _currentUser = null;
        if (_encryptionKey != null)
        {
            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
        }
        _encryptionKey = null;
        OnSessionChanged?.Invoke();
    }

    public void RecordActivity()
    {
        if (IsAuthenticated)
        {
            _lastActivityTime = DateTime.UtcNow;
        }
    }

    public void Lock()
    {
        if (_currentUser != null && _encryptionKey != null)
        {
            Array.Clear(_encryptionKey, 0, _encryptionKey.Length);
            _encryptionKey = null;
            OnSessionChanged?.Invoke();
        }
    }

    public void Unlock(byte[] encryptionKey)
    {
        if (_currentUser != null)
        {
            _encryptionKey = encryptionKey;
            _lastActivityTime = DateTime.UtcNow;
            OnSessionChanged?.Invoke();
        }
    }

    public double GetTimeUntilLock()
    {
        if (!IsAuthenticated || _currentUser == null) return 0;
        var elapsed = DateTime.UtcNow.Subtract(_lastActivityTime).TotalMinutes;
        var remaining = _inactivityTimeoutMinutes - elapsed;
        return Math.Max(0, remaining);
    }
}
```

## Old project bug history (context only — do not re-apply as fixes here)

The earlier AI-generated project at `Secure_Vault/` went through this bug
sequence before the rebuild decision was made. Useful as "lessons learned,"
already baked into the plan above — no need to re-diagnose:

1. Create Account button stayed disabled (missing `@bind:event="oninput"`).
2. Zero interactivity — old-style `App.razor` (`<Router>` only, no HTML
   shell, no `blazor.web.js`).
3. Duplicate nested `<html>` documents (`MainLayout.razor` also had a full
   shell).
4. Login succeeded then bounced back — per-page render mode caused new
   circuits per navigation, wiping Scoped session state. (→ this is why the
   new build uses global interactivity from the start.)
5. Malformed `</head` tag from a hand-edit.
6. Missing `<base href="/" />`.
7. Unresolved when rebuild was decided: `TaskCanceledException` in
   `RemoteNavigationManager.PerformNavigationAsync` after login, circuit
   died, browser fell back to a raw HTML form GET. Never fully root-caused;
   decided it was likely a downstream symptom of the shell-file corruption
   from bugs 2–3 and 5–6 rather than an independent issue.

## User context (for tone/approach)

- College student (6th semester, software engineering/IT program), Nepal.
- **Plans to use THIS Claude account specifically for cybersecurity work
  later**, so is deliberately swapping to a different account now to avoid
  burning this one's daily limit on the SecureVault project. Continuing
  SecureVault on a fresh account from this handoff doc.
- Wants a **professional-looking UI** for this rebuild — old project's UI
  was plain default Bootstrap, explicitly wants this one to look better.
- Prefers building file-by-file with complete code per stage, confirming
  each stage builds/works before moving to the next (has been doing
  `dotnet build` after each stage so far — keep this pattern).
- Appreciates direct, evidence-based answers over speculative fixes, based
  on friction earlier in the debugging session on the old project.
