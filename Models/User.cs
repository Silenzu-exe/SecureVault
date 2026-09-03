namespace SecureVault.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MasterPasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}