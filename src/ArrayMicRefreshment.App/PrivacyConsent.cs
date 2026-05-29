using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

internal static class PrivacyConsent
{
    public static bool EnsureAccepted(AppSettings settings, string apiBaseUrl, IWin32Window? owner)
    {
        if (!settings.PromptRefineEnabled)
        {
            return true;
        }

        if (!PrivacyConfirmation.TryResolveHost(apiBaseUrl, out var host))
        {
            return true;
        }

        if (PrivacyConfirmation.IsLoopbackHost(host))
        {
            settings.PrivacyAcceptedHost = host;
            return true;
        }

        if (!PrivacyConfirmation.ShouldPromptForHost(apiBaseUrl, settings.PrivacyAcceptedHost))
        {
            return true;
        }

        // Headless CI / unit tests: never block on a modal dialog when there is no owner window.
        if (owner is null)
        {
            return false;
        }

        var message = $"提示词整理将把识别文本发送到 {host}。继续？";
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
}
