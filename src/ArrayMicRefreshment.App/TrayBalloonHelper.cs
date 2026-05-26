namespace ArrayMicRefreshment.App;

/// <summary>
/// Windows tray balloons are single-flight — a visible tip blocks the next one until it closes.
/// Use <see cref="DismissForNextTask"/> when a new voice/pipeline step starts so work is never
/// queued behind an old balloon. Pending <see cref="Show"/> calls are cancelled automatically.
/// </summary>
internal sealed class TrayBalloonHelper : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly SynchronizationContext _uiContext;
    private int _epoch;
    private bool _balloonShowing;

    public TrayBalloonHelper(NotifyIcon icon, SynchronizationContext uiContext)
    {
        _icon = icon;
        _uiContext = uiContext;
    }

    /// <summary>
    /// Immediately dismiss any visible balloon on the UI thread and invalidate queued Shows.
    /// Call at the start of each new voice task (wake → dictation → ASR, PTT press, etc.).
    /// </summary>
    public void DismissForNextTask()
    {
        Interlocked.Increment(ref _epoch);
        RunOnUiSync(ForceDismissVisual);
    }

    public void Show(int timeoutMs, string title, string text, ToolTipIcon icon)
    {
        var epoch = Volatile.Read(ref _epoch);
        RunOnUiAsync(() =>
        {
            if (epoch != Volatile.Read(ref _epoch))
            {
                return;
            }

            ForceDismissVisual();
            if (epoch != Volatile.Read(ref _epoch))
            {
                return;
            }

            _icon.ShowBalloonTip(timeoutMs, title, text, icon);
            _balloonShowing = true;
        });
    }

    public void Dispose()
    {
        DismissForNextTask();
    }

    private void ForceDismissVisual()
    {
        _balloonShowing = false;
        if (!_icon.Visible)
        {
            return;
        }

        // Toggling Visible is the reliable way to dismiss a tray balloon immediately on Windows.
        _icon.Visible = false;
        _icon.Visible = true;
    }

    private void RunOnUiSync(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Send(_ => action(), null);
    }

    private void RunOnUiAsync(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
    }
}
