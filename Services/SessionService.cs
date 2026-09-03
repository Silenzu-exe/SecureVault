using SecureVault.Models;

namespace SecureVault.Services;

/// <summary>
/// Manages session state: current user, in-memory encryption key, and
/// auto-lock. Registered as Scoped — under global interactivity this
/// lives for the whole circuit/session, which is exactly what we want here.
/// </summary>
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
        if (!IsAuthenticated || _currentUser == null)
            return 0;

        var elapsed = DateTime.UtcNow.Subtract(_lastActivityTime).TotalMinutes;
        var remaining = _inactivityTimeoutMinutes - elapsed;
        return Math.Max(0, remaining);
    }
}