using Microsoft.AspNetCore.Components;
using SecureVault.Services;

namespace SecureVault.Components.Pages;

public abstract class AuthorizedPageBase : ComponentBase, IDisposable
{
    [Inject] protected SessionService Session { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    private PeriodicTimer? _lockCheckTimer;
    private CancellationTokenSource? _cts;

    protected override void OnInitialized()
    {
        Session.RecordActivity();

        if (Session.CurrentUser is null)
        {
            Nav.NavigateTo("/login", forceLoad: false);
            return;
        }

        if (Session.IsLocked)
        {
            Nav.NavigateTo("/unlock", forceLoad: false);
            return;
        }

        StartLockWatcher();
    }

    private void StartLockWatcher()
    {
        _cts = new CancellationTokenSource();
        _lockCheckTimer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        _ = WatchForLockAsync(_cts.Token);
    }

    private async Task WatchForLockAsync(CancellationToken token)
    {
        try
        {
            while (await _lockCheckTimer!.WaitForNextTickAsync(token))
            {
                if (Session.IsLocked)
                {
                    Session.Lock();
                    await InvokeAsync(() => Nav.NavigateTo("/unlock", forceLoad: false));
                    return;
                }
            }
        }
        catch (OperationCanceledException) { /* expected on navigation away */ }
        catch (ObjectDisposedException) { /* expected on dispose mid-wait */ }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _lockCheckTimer?.Dispose();
    }
}