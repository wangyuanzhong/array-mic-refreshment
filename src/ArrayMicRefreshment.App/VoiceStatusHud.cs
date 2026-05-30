using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

/// <summary>
/// Native HUD — paints on the form client area (no child Label/WebView that clip vertically).
/// </summary>
internal sealed class VoiceStatusHud : Form, IVoiceStatusHud
{
    private const int HideDelayMs = 120;
    private const int MarginPx = 16;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly System.Windows.Forms.Timer _hideTimer;
    private VoiceActivityPhase _phase = VoiceActivityPhase.Idle;
    private HudScreenCorner _corner = HudScreenCorner.BottomRight;
    private string _message = string.Empty;
    private Color _textColor = DesignTokens.HudText;

    public VoiceStatusHud()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Padding = Padding.Empty;
        BackColor = DesignTokens.HudBackground;
        Opacity = DesignTokens.HudOpacity;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ApplyClientSizeForDpi();

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

    IntPtr IVoiceStatusHud.Handle => IsHandleCreated ? base.Handle : IntPtr.Zero;

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

        _message = message;
        _textColor = phase switch
        {
            VoiceActivityPhase.Error => DesignTokens.HudError,
            VoiceActivityPhase.WakePrompt => DesignTokens.HudWake,
            VoiceActivityPhase.Recording or VoiceActivityPhase.Recognizing => DesignTokens.HudAccent,
            _ => DesignTokens.HudText,
        };

        Invalidate();

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

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyClientSizeForDpi();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using (var borderPen = new Pen(Color.FromArgb(128, 126, 200, 227)))
        using (var bg = new SolidBrush(DesignTokens.HudBackground))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var round = Math.Min(12, rect.Height / 3);
            using var path = RoundedRect(rect, round);
            g.FillPath(bg, path);
            g.DrawPath(borderPen, path);
        }

        var textRect = new Rectangle(
            HudLayout.HorizontalInset,
            HudLayout.VerticalInset,
            Math.Max(0, rect.Width - HudLayout.HorizontalInset * 2),
            Math.Max(0, rect.Height - HudLayout.VerticalInset * 2));

        TextRenderer.DrawText(
            g,
            _message,
            DesignTokens.HudFont,
            textRect,
            _textColor,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.NoPadding);
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

    private void ApplyClientSizeForDpi()
    {
        var scale = DeviceDpi / 96f;
        var w = (int)Math.Round(HudLayout.LogicalWidth * scale);
        var h = (int)Math.Round(HudLayout.LogicalHeight * scale);
        ClientSize = new Size(Math.Max(w, HudLayout.LogicalWidth), Math.Max(h, HudLayout.LogicalHeight));
    }

    private void Reposition()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
        var x = _corner is HudScreenCorner.TopRight or HudScreenCorner.BottomRight
            ? area.Right - ClientSize.Width - MarginPx
            : area.Left + MarginPx;
        var y = _corner is HudScreenCorner.TopLeft or HudScreenCorner.TopRight
            ? area.Top + MarginPx
            : area.Bottom - ClientSize.Height - MarginPx;
        Location = new Point(x, y);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
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
