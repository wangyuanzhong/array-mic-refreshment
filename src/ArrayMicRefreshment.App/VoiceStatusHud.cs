using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

/// <summary>Small always-on-top overlay for fast in-session status (replaces mid-flow tray balloons).</summary>
internal sealed class VoiceStatusHud : Form
{
    private const int HideDelayMs = 120;
    private const int MarginPx = 16;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _hideTimer;
    private VoiceActivityPhase _phase = VoiceActivityPhase.Idle;
    private HudScreenCorner _corner = HudScreenCorner.BottomRight;

    public VoiceStatusHud()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(300, 52);
        BackColor = DesignTokens.HudBackground;
        Opacity = DesignTokens.HudOpacity;
        Padding = new Padding(12, 10, 12, 10);

        _label = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.HudBackground,
            ForeColor = DesignTokens.HudText,
            Font = DesignTokens.HudFont,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        Controls.Add(_label);

        _hideTimer = new System.Windows.Forms.Timer { Interval = HideDelayMs };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (!Visible || _phase == VoiceActivityPhase.Idle)
            {
                Hide();
            }
        };
    }

    public VoiceActivityPhase Phase => _phase;

    public void SetCorner(HudScreenCorner corner)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetCorner(corner));
            return;
        }

        if (_corner == corner)
        {
            return;
        }

        _corner = corner;
        if (Visible)
        {
            Reposition();
        }
    }

    public void SetPhase(VoiceActivityPhase phase, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetPhase(phase, message));
            return;
        }

        _phase = phase;
        _hideTimer.Stop();

        if (phase == VoiceActivityPhase.Idle || string.IsNullOrWhiteSpace(message))
        {
            Hide();
            return;
        }

        _label.Text = message;
        _label.ForeColor = phase switch
        {
            VoiceActivityPhase.Error => DesignTokens.HudError,
            VoiceActivityPhase.Recording or VoiceActivityPhase.Recognizing or VoiceActivityPhase.WakePrompt
                => DesignTokens.HudAccent,
            _ => DesignTokens.HudText,
        };

        if (!Visible)
        {
            Reposition();
            Show();
        }
    }

    public void HideSoon()
    {
        if (InvokeRequired)
        {
            BeginInvoke(HideSoon);
            return;
        }

        _phase = VoiceActivityPhase.Idle;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    private void Reposition()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
        var x = _corner is HudScreenCorner.TopRight or HudScreenCorner.BottomRight
            ? area.Right - Width - MarginPx
            : area.Left + MarginPx;
        var y = _corner is HudScreenCorner.TopLeft or HudScreenCorner.TopRight
            ? area.Top + MarginPx
            : area.Bottom - Height - MarginPx;
        Location = new Point(x, y);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
