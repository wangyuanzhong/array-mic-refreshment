using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;

namespace ArrayMicRefreshment.App.Web;

/// <summary>WinForms shell hosting the unified Web UI (Route B). WebView2 is created on demand when the form loads.</summary>
public sealed class WebUiHostForm : Form
{
    private const int DefaultLogicalWidth = 960;
    private const int DefaultLogicalHeight = 720;
    private const int MinLogicalWidth = 640;
    private const int MinLogicalHeight = 480;

    private readonly WebUiBridgeContext? _context;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private WebUiBridge? _bridge;
    private string _hashRoute;
    private int _logicalWidth = DefaultLogicalWidth;
    private int _logicalHeight = DefaultLogicalHeight;

    /// <summary>When true, user closing the window hides it instead of disposing (settings singleton).</summary>
    public bool HideOnClose { get; set; }

    public WebUiHostForm(string hashRoute = "#/settings")
        : this(hashRoute, null)
    {
    }

    public WebUiHostForm(string hashRoute, WebUiBridgeContext? context)
    {
        _hashRoute = NormalizeHashRoute(hashRoute);
        _context = context;
        if (_context is not null)
        {
            _context.HostForm = this;
        }

        Text = TitleForRoute(_hashRoute);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ResolveInitialLogicalSize(context);
        ApplyDpiLayout();
        TopMost = false;
        Controls.Add(_webView);

        Load += OnFormLoad;
        ResizeEnd += OnResizeEnd;
        FormClosing += OnFormClosing;
    }

    private void ResolveInitialLogicalSize(WebUiBridgeContext? context)
    {
        if (context?.Settings is null)
        {
            _logicalWidth = DefaultLogicalWidth;
            _logicalHeight = DefaultLogicalHeight;
            return;
        }

        var w = context.Settings.SettingsWindowWidth;
        var h = context.Settings.SettingsWindowHeight;
        _logicalWidth = w >= MinLogicalWidth ? w : DefaultLogicalWidth;
        _logicalHeight = h >= MinLogicalHeight ? h : DefaultLogicalHeight;
    }

    private void ApplyDpiLayout()
    {
        var scale = WebViewDpiScaling.GetScale(this);
        MinimumSize = WebViewDpiScaling.ScaleLogicalSize(MinLogicalWidth, MinLogicalHeight, scale);
        ClientSize = WebViewDpiScaling.ScaleLogicalSize(_logicalWidth, _logicalHeight, scale);
        WebViewDpiScaling.ApplyToWebView(_webView, this);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyDpiLayout();
    }

    private void OnResizeEnd(object? sender, EventArgs e) => PersistClientSize();

    private void PersistClientSize()
    {
        if (_context?.Settings is null || _context.SettingsStore is null)
        {
            return;
        }

        if (!IsHandleCreated || WindowState != FormWindowState.Normal)
        {
            return;
        }

        var scale = WebViewDpiScaling.GetScale(this);
        var logical = WebViewDpiScaling.ToLogicalSize(ClientSize, scale);
        if (logical.Width < MinLogicalWidth || logical.Height < MinLogicalHeight)
        {
            return;
        }

        _logicalWidth = logical.Width;
        _logicalHeight = logical.Height;

        if (_context.Settings.SettingsWindowWidth == logical.Width
            && _context.Settings.SettingsWindowHeight == logical.Height)
        {
            return;
        }

        _context.Settings.SettingsWindowWidth = logical.Width;
        _context.Settings.SettingsWindowHeight = logical.Height;
        _context.SettingsStore.Save(_context.Settings);
    }

    public void NavigateTo(string hashRoute)
    {
        _hashRoute = NormalizeHashRoute(hashRoute);
        Text = TitleForRoute(_hashRoute);
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        var targetUrl = WebUiConstants.HashUrl(_hashRoute);
        var currentUrl = _webView.Source?.AbsoluteUri;
        if (!string.IsNullOrEmpty(currentUrl)
            && string.Equals(currentUrl, targetUrl, StringComparison.OrdinalIgnoreCase))
        {
            RequestSettingsPageRefresh();
            return;
        }

        _webView.CoreWebView2.Navigate(targetUrl);
    }

    /// <summary>Reload settings data in the SPA without a full document navigation (fast reopen).</summary>
    public void RequestSettingsPageRefresh()
    {
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        _ = _webView.CoreWebView2.ExecuteScriptAsync(
            "if (typeof window.__amrRefreshSettings==='function') void window.__amrRefreshSettings();");
    }

    private async void OnFormLoad(object? sender, EventArgs e)
    {
        Load -= OnFormLoad;

        if (!WebView2RuntimeChecker.IsRuntimeAvailable(out _))
        {
            MessageBox.Show(
                this,
                WebView2RuntimeChecker.MissingRuntimeMessage,
                "需要 WebView2 运行时",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        try
        {
            await _webView.EnsureCoreWebView2Async();
            ApplyDpiLayout();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WebView2 EnsureCoreWebView2Async failed");
            MessageBox.Show(
                this,
                $"WebView2 初始化失败：{ex.Message}\n\n{WebView2RuntimeChecker.MissingRuntimeMessage}",
                "WebView2 错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        if (_context is not null)
        {
            _bridge = new WebUiBridge(_context);
            _webView.CoreWebView2.AddHostObjectToScript("amr", _bridge);
        }

#if DEBUG
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

        var wwwRootDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var indexPath = Path.Combine(wwwRootDir, "index.html");
        if (!File.Exists(indexPath))
        {
            MessageBox.Show(
                this,
                $"未找到 Web UI 文件：{indexPath}\n\n请先运行 ui 目录下的 npm run build。",
                "Web UI 缺失",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

        try
        {
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                WebUiConstants.WwwRootVirtualHost,
                wwwRootDir,
                CoreWebView2HostResourceAccessKind.Allow);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SetVirtualHostNameToFolderMapping failed for {Dir}", wwwRootDir);
            MessageBox.Show(
                this,
                $"无法映射 Web UI 目录：{ex.Message}",
                "Web UI 错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        var navigateUrl = WebUiConstants.HashUrl(_hashRoute);
        _webView.CoreWebView2.Navigate(navigateUrl);
        Log.Information("WebUiHostForm navigating to {Uri} (wwwroot={Dir})", navigateUrl, wwwRootDir);
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            return;
        }

        Log.Warning(
            "WebUiHostForm navigation failed: status={Status} error={Error}",
            e.HttpStatusCode,
            e.WebErrorStatus);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (HideOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            PersistClientSize();
            e.Cancel = true;
            Hide();
            return;
        }

        if (_context is not null)
        {
            _context.HostForm = null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView.Dispose();
        }

        base.Dispose(disposing);
    }

    private static string NormalizeHashRoute(string hashRoute)
    {
        if (string.IsNullOrWhiteSpace(hashRoute))
        {
            return "#/settings";
        }

        if (hashRoute.StartsWith('#'))
        {
            return hashRoute.StartsWith("#/") ? hashRoute : "#/" + hashRoute.TrimStart('#');
        }

        return hashRoute.StartsWith('/') ? $"#{hashRoute}" : $"#/{hashRoute}";
    }

    private static string TitleForRoute(string hashRoute)
    {
        var path = hashRoute.Split('?', StringSplitOptions.RemoveEmptyEntries)[0];
        return path switch
        {
            "#/enroll" => "Array Mic — 注册说话人",
            "#/privacy" => "Array Mic — 隐私确认",
            "#/onboarding" => "Array Mic — 欢迎使用",
            _ => $"Array Mic — 设置 ({AppInfo.Version})",
        };
    }
}
