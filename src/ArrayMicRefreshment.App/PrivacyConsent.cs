namespace ArrayMicRefreshment.App;

internal static class PrivacyConsent
{
    public static bool EnsureAccepted(Core.AppSettings settings, string apiBaseUrl, IWin32Window? owner)
    {
        if (!settings.PromptRefineEnabled)
        {
            return true;
        }

        if (!TryGetRemoteHost(apiBaseUrl, out var host))
        {
            return true;
        }

        if (string.Equals(settings.PrivacyAcceptedHost, host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var message =
            $"启用提示词整理后，识别文字将发送到远程 API：\n{host}\n\n是否继续？";
        var result = MessageBox.Show(
            owner,
            message,
            "隐私确认",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            settings.PrivacyAcceptedHost = host;
            return true;
        }

        return false;
    }

    public static bool TryGetRemoteHost(string apiBaseUrl, out string host)
    {
        host = string.Empty;
        if (!Uri.TryCreate(apiBaseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.IsLoopback || IsLocalHost(uri.Host))
        {
            return false;
        }

        host = uri.Host;
        return true;
    }

    private static bool IsLocalHost(string name) =>
        name.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}
