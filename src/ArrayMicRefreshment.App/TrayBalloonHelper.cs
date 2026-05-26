namespace ArrayMicRefreshment.App;

/// <summary>
/// Tray balloons are single-flight on Windows — use only for final transcript output and errors.
/// Live session status uses <see cref="VoiceFeedbackPresenter"/> + HUD instead.
/// </summary>
internal sealed class TrayBalloonHelper : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly SynchronizationContext _uiContext;
    private int _epoch;

    public TrayBalloonHelper(NotifyIcon icon, SynchronizationContext uiContext)
    {
        _icon = icon;
        _uiContext = uiContext;
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
        });
    }

    public void DismissForNextTask()
    {
        Interlocked.Increment(ref _epoch);
        RunOnUiSync(ForceDismissVisual);
    }

    public void Dispose() => DismissForNextTask();

    private void ForceDismissVisual()
    {
        if (!_icon.Visible)
        {
            return;
        }

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
