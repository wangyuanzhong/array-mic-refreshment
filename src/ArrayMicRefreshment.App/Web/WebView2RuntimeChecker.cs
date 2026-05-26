using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.App.Web;

/// <summary>
/// Detects Evergreen WebView2 Runtime availability and guides the user when missing.
/// See docs/UI_ROUTE_B_WEBVIEW2.md §9.3.
/// </summary>
public static class WebView2RuntimeChecker
{
    public const string InstallDocumentationUrl =
        "https://developer.microsoft.com/microsoft-edge/webview2/";

    public const string WingetInstallCommand =
        "winget install Microsoft.EdgeWebView2Runtime";

    /// <summary>User-facing message when WebView2 Runtime is not installed.</summary>
    public const string MissingRuntimeMessage =
        "未检测到 Microsoft Edge WebView2 运行时，无法打开 Web 界面。\n\n" +
        "请安装 WebView2 Evergreen Runtime 后重试：\n" +
        "• 运行：winget install Microsoft.EdgeWebView2Runtime\n" +
        "• 或访问 Microsoft 官方下载页获取安装程序\n\n" +
        "安装完成后请重新启动 Array Mic Refreshment。";

    public static bool IsRuntimeAvailable(out string? version)
    {
        version = null;
#if WINDOWS
        try
        {
            version = Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .GetAvailableBrowserVersionString(null);
            return !string.IsNullOrWhiteSpace(version);
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }

    public static bool IsRuntimeAvailable() => IsRuntimeAvailable(out _);

    /// <summary>Shows the standard missing-runtime dialog (optionally opens install page).</summary>
    public static void ShowRuntimeMissingDialog(IWin32Window? owner = null)
    {
        TryEnsureAvailable(owner);
    }

    /// <summary>
    /// Shows a MessageBox when runtime is missing. Returns true if runtime is available.
    /// </summary>
    public static bool TryEnsureAvailable(IWin32Window? owner)
    {
        if (IsRuntimeAvailable(out _))
        {
            return true;
        }

        var result = MessageBox.Show(
            owner,
            MissingRuntimeMessage,
            "需要 WebView2 运行时",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning);

        if (result == DialogResult.OK)
        {
            TryOpenInstallPage();
        }

        return false;
    }

    public static void TryOpenInstallPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = InstallDocumentationUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open WebView2 install page: {ex.Message}");
        }
    }
}
