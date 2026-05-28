using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;

namespace ArrayMicRefreshment.App.Web;

/// <summary>WinForms shell hosting the unified Web UI (Route B). WebView2 is created on demand when the form loads.</summary>
public sealed class WebUiHostForm : Form
{
    /// <summary>Virtual host for local wwwroot (avoid file:// — ES modules often stay blank).</summary>
    private const string WwwRootVirtualHost = "amr.local";

    private readonly string _hashRoute;
    private readonly WebUiBridgeContext? _context;
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private WebUiBridge? _bridge;

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
        MinimumSize = new Size(640, 480);
        ClientSize = new Size(960, 720);
        TopMost = false;
        Controls.Add(_webView);

        Load += OnFormLoad;
        FormClosing += OnFormClosing;
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
                WwwRootVirtualHost,
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

        // https://amr.local/... serves files from wwwroot; file:// breaks Vite ES module bundles (white screen).
        var navigateUrl = $"https://{WwwRootVirtualHost}/index.html{_hashRoute}";
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
