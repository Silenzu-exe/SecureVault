# SecureVault - Password Manager & Digital Vault

A secure, modern password manager built with ASP.NET Core Blazor Server (.NET 10) featuring end-to-end encryption with AES-256.

## Features

### 🔐 Security
- **Master Password Authentication**: Custom authentication system (no external auth libraries)
- **AES-256 Encryption**: All passwords encrypted with 256-bit AES in CBC mode
- **PBKDF2 Key Derivation**: 100,000 iterations with SHA256 for secure key derivation
- **Cryptographically Secure Random**: Using `System.Security.Cryptography.RandomNumberGenerator`
- **In-Memory Keys**: Encryption keys never persisted to disk
- **Auto-Lock**: Configurable inactivity timeout (default 5 minutes)

### 💾 Vault Management
- **Add/Edit/Delete**: Full CRUD operations for credential entries
- **Entry Details**: Site name, username, encrypted password, optional notes
- **Secure Display**: Passwords masked by default with reveal/copy buttons
- **On-Demand Decryption**: Passwords decrypted only when needed

### 🎲 Password Generator
- **Configurable Options**:
  - Custom password length (8-64 characters)
  - Character set selection (lowercase, uppercase, digits, symbols)
- **Real-Time Strength Analysis**:
  - Entropy calculation
  - Strength rating (Weak → Fair → Good → Excellent)
  - Live visualization
- **Password Testing**: Analyze any password's strength

### ⚙️ Settings & Management
- **Change Master Password**: Securely update master password with full re-encryption of vault
- **Auto-Lock Timeout**: Configurable session timeout (1-60 minutes)
- **Account Information**: View user ID and creation date

## Architecture

### Layered Design
```
/Models              - Entity classes (User, VaultEntry)
/Data                - DbContext, EF Core migrations
/Services            - Business logic
  ├─ EncryptionService      - AES-256, PBKDF2, salt/IV generation
  ├─ AuthService            - User authentication, master password management
  ├─ VaultService           - CRUD operations for vault entries
  ├─ PasswordGeneratorService - Password generation & strength analysis
  └─ SessionService          - Session state & auto-lock management
/Components/Pages    - Routable Blazor pages
/Components/Layout   - Shared layout components
```

### Database Schema

**Users Table**
- `Id` (Guid, PK)
- `MasterPasswordHash` (string)
- `Salt` (string, Base64)
- `CreatedAt` (DateTime)

**VaultEntries Table**
- `Id` (Guid, PK)
- `UserId` (Guid, FK)
- `SiteName` (string)
- `Username` (string)
- `EncryptedPassword` (string, Base64)
- `IV` (string, Base64)
- `Notes` (string, nullable)
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime)

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQLite support (included)

### Installation & Running

1. **Restore dependencies**:
   ```bash
   cd Secure_Vault
   dotnet restore
   ```

2. **Apply migrations** (automatic on startup, or manual):
   ```bash
   export PATH="$PATH:~/.dotnet/tools"
   dotnet ef database update
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

4. **Access the app**:
   - Open browser to `https://localhost:5001` (or `http://localhost:5000`)
   - First-time users: Click "Get Started" to create master password
   - Returning users: Log in with master password

### Configuration

**Database Connection** (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  }
}
```

**HTTPS/Development** (`appsettings.Development.json`):
- Uses self-signed certificate in development
- Configure production certificate for deployment

## Pages & Workflows

### Authentication
- **Index.razor** (`/`): Login/landing page
- **Setup.razor** (`/setup`): First-run master password creation
- **Unlock.razor** (`/unlock`): Session unlock after inactivity

### Core Features
- **Vault.razor** (`/vault`): Main vault, CRUD management
- **Generator.razor** (`/generator`): Password generation & strength testing
- **Settings.razor** (`/settings`): Master password change, timeout config

### Layout
- **MainLayout.razor**: Navigation bar, session status, authenticated UI
- **App.razor**: Root component with routing configuration

## Security Considerations

### What We Protect
✅ Master password (hashed with PBKDF2, never stored plaintext)  
✅ Vault passwords (encrypted with AES-256)  
✅ Encryption keys (stored in memory only)  
✅ IV values (unique per entry, stored alongside ciphertext)  

### What We Don't
- External authentication libraries (custom implementation)
- Two-factor authentication (not implemented)
- Biometric authentication (not implemented)
- Secure password deletion from memory (relies on .NET GC)

### Best Practices Implemented
- Constant-time string comparison for password verification
- Cryptographically secure random number generation
- Proper salt/IV generation (16 bytes each)
- PBKDF2 with 100,000 iterations (NIST recommendation)
- AES-256 with CBC mode + PKCS7 padding
- Transaction support for atomic master password changes

## Development

### Key Components
- **Blazor Server**: Interactive server-side rendering
- **Entity Framework Core**: ORM with SQLite
- **Bootstrap 5**: Responsive UI styling
- **System.Security.Cryptography**: All crypto operations

### No External Dependencies
- No authentication NuGet packages
- No password manager libraries
- Only dependencies:
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.EntityFrameworkCore.Design`
  - `Microsoft.EntityFrameworkCore.Tools`

### Building & Publishing

**Development Build**:
```bash
dotnet build
```

**Release Build**:
```bash
dotnet publish -c Release
```

**Run Production Build**:
```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet Secure_Vault.dll
```

## Database Migrations

**View existing migrations**:
```bash
dotnet ef migrations list
```

**Create new migration** (after model changes):
```bash
dotnet ef migrations add MigrationName
```

**Apply migrations**:
```bash
dotnet ef database update
```

**Revert to previous migration**:
```bash
dotnet ef database update PreviousMigrationName
```

## Troubleshooting

### Port Already in Use
```bash
dotnet run --urls "https://localhost:5002"
```

### Database Locked
Remove `app.db` and restart (will recreate on first run):
```bash
rm app.db
dotnet run
```

### HTTPS Certificate Issues
In development, accept self-signed certificate or run over HTTP:
```bash
dotnet run --urls "http://localhost:5000"
```

### Missing EF Tools
```bash
dotnet tool install --global dotnet-ef
export PATH="$PATH:~/.dotnet/tools"
```

## Future Enhancements
- [ ] Password breach checking
- [ ] Password history tracking
- [ ] Folder/category organization
- [ ] Sharing vault entries with others
- [ ] Mobile app (Maui)
- [ ] Browser extension
- [ ] Cloud sync with encryption
- [ ] Passwordless authentication
- [ ] Face/fingerprint unlock
- [ ] Import from other password managers

## Security Disclaimer
SecureVault is a demonstration project. For production use with sensitive data:
- Have security audit performed
- Implement additional features (2FA, secure enclave support, etc.)
- Consider compliance requirements (GDPR, HIPAA, etc.)
- Use established password managers with security track records

## License
Open source educational project.

## Author
Built with GitHub Copilot - Secure password management demonstration.
# Secure_vault
HEAD
# Secure_vault
bb98f751fe0a00c9f7c247204929349e99a13810
