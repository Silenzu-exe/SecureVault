namespace SecureVault.Services;

public class ScrollTrackingService
{
    public string ActiveSection { get; private set; } = "hero";
    public event Action? OnSectionChanged;

    public void SetActiveSection(string section)
    {
        if (ActiveSection == section) return;
        ActiveSection = section;
        OnSectionChanged?.Invoke();
    }
}