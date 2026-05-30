using System.Text.Json;
using ArrayMicRefreshment.App.Web;
using ArrayMicRefreshment.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;

namespace ArrayMicRefreshment.App;

/// <summary>
/// Transparent WebView2 overlay for live voice status.
/// Uses standalone <c>hud.html</c> (not the settings SPA) to avoid 100vh / router layout bugs.
/// </summary>
internal sealed class VoiceWebStatusHud : Form, IVoiceStatusHud
{
    private const int HideDelayMs = 120;
    private const int MarginPx = 16;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _hideTimer;
    private CoreWebView2? _core;
    private VoiceActivityPhase _phase = VoiceActivityPhase.Idle;
    private HudScreenCorner _corner = HudScreenCorner.BottomRight;
    private string _pendingMessage = string.Empty;
    private bool _navigationReady;
    private Task? _initTask;
    private bool _initFailed;
    private bool _coreInitialized;

    private VoiceWebStatusHud()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Padding = Padding.Empty;
        var scale = DeviceDpi / 96f;
        ClientSize = new Size(
            (int)Math.Round(HudLayout.LogicalWidth * scale),
            (int)Math.Round(HudLayout.LogicalHeight * scale));
        BackColor = Color.Transparent;
        Opacity = 0.99;
        Controls.Add(_webView);
        SyncWebViewBounds();

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

    public static VoiceWebStatusHud CreateSynchronously() => new();

    public void BeginInitialization()
    {
        if (_coreInitialized || _initFailed || _initTask is not null)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(BeginInitialization);
            return;
        }

        _initTask = InitializeCoreWebViewAsync();
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
            PostToWeb(phase, string.Empty);
            Hide();
            return;
        }

        if (_initFailed)
        {
            return;
        }

        _pendingMessage = message;
        if (!_coreInitialized)
        {
            BeginInitialization();
            return;
        }

        PostToWeb(phase, message);

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

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SyncWebViewBounds();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        SyncWebViewBounds();
    }

    private void SyncWebViewBounds()
    {
        _webView.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
    }

    private async Task InitializeCoreWebViewAsync()
    {
        try
        {
            await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);

            _core = _webView.CoreWebView2;
            _webView.ZoomFactor = 1.0;
            _webView.DefaultBackgroundColor = Color.Transparent;
            _core.Settings.AreDefaultContextMenusEnabled = false;
            _core.Settings.AreDevToolsEnabled = false;
            _core.Settings.IsStatusBarEnabled = false;
            _core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _core.NavigationCompleted += OnNavigationCompleted;

            var wwwRootDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var hudPath = Path.Combine(wwwRootDir, "hud.html");
            if (!File.Exists(hudPath))
            {
                throw new FileNotFoundException("Web HUD hud.html missing", hudPath);
            }

            _core.SetVirtualHostNameToFolderMapping(
                WebUiConstants.WwwRootVirtualHost,
                wwwRootDir,
                CoreWebView2HostResourceAccessKind.Allow);

            _core.Navigate(WebUiConstants.HudOverlayUrl);
            _coreInitialized = true;

            if (_phase != VoiceActivityPhase.Idle && !string.IsNullOrWhiteSpace(_pendingMessage))
            {
                Reposition();
                Show();
                PostToWeb(_phase, _pendingMessage);
            }
        }
        catch (Exception ex)
        {
            _initFailed = true;
            Log.Warning(ex, "Voice Web HUD init failed; tray icon feedback still works (set AMR_WEB_HUD=0 for native HUD)");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            Log.Warning("Voice Web HUD navigation failed: {Error}", e.WebErrorStatus);
            return;
        }

        _navigationReady = true;
        if (_phase != VoiceActivityPhase.Idle && !string.IsNullOrWhiteSpace(_pendingMessage))
        {
            PostToWeb(_phase, _pendingMessage);
        }
    }

    private void PostToWeb(VoiceActivityPhase phase, string message)
    {
        if (_core is null || !_navigationReady)
        {
            return;
        }

        try
        {
            var payload = JsonSerializer.Serialize(new { phase = phase.ToString(), message });
            _core.PostWebMessageAsJson(payload);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Voice Web HUD PostWebMessageAsJson failed");
        }
    }

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
            if (_core is not null)
            {
                _core.NavigationCompleted -= OnNavigationCompleted;
            }

            _hideTimer.Dispose();
            _webView.Dispose();
        }

        base.Dispose(disposing);
    }
}
