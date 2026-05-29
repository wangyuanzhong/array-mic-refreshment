using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

/// <summary>Tray icon state + HUD for live voice phases; balloons are handled elsewhere.</summary>
internal sealed class VoiceFeedbackPresenter : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly IVoiceStatusHud _hud;
    private readonly SynchronizationContext _uiContext;
    private VoiceActivityPhase _phase = VoiceActivityPhase.Idle;
    private string _activityHint = string.Empty;

    private readonly Action? _onPhaseChanged;

    public VoiceFeedbackPresenter(
        NotifyIcon trayIcon,
        SynchronizationContext uiContext,
        AppSettings settings,
        Action? onPhaseChanged = null)
    {
        _trayIcon = trayIcon;
        _uiContext = uiContext;
        _onPhaseChanged = onPhaseChanged;
        _hud = VoiceStatusHudFactory.Create(settings);
        ApplyTrayIcon(VoiceActivityPhase.Idle);
    }

    public VoiceActivityPhase Phase => _phase;

    public string ActivityHint => _activityHint;

    public IntPtr HudHandle => _hud.Handle;

    public void ApplyHudCorner(HudScreenCorner corner)
    {
        RunOnUi(() => _hud.SetCorner(corner));
    }

    public void SetPhase(VoiceActivityPhase phase, string? hudMessage = null)
    {
        RunOnUi(() =>
        {
            _phase = phase;
            _activityHint = hudMessage ?? phase switch
            {
                VoiceActivityPhase.WakePrompt => "已唤醒",
                VoiceActivityPhase.Recording => "录音中",
                VoiceActivityPhase.Recognizing => "识别中",
                VoiceActivityPhase.Error => "出错",
                _ => string.Empty,
            };

            ApplyTrayIcon(phase);

            if (phase == VoiceActivityPhase.Idle)
            {
                _hud.SetPhase(VoiceActivityPhase.Idle, string.Empty);
                _onPhaseChanged?.Invoke();
                return;
            }

            var message = hudMessage ?? _activityHint;
            _hud.SetPhase(phase, message);
            _onPhaseChanged?.Invoke();
        });
    }

    public void ClearSession()
    {
        SetPhase(VoiceActivityPhase.Idle);
    }

    public void Dispose()
    {
        RunOnUiSync(() =>
        {
            if (_hud is Form form)
            {
                form.Close();
            }

            _hud.Dispose();
        });
    }

    private void ApplyTrayIcon(VoiceActivityPhase phase)
    {
        _trayIcon.Icon = TrayIconFactory.ForPhase(phase);
    }

    private void RunOnUi(Action action)
    {
        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(_ => action(), null);
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
}
