using System.Text.Json;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Speaker;

namespace ArrayMicRefreshment.App.Web;

/// <summary>Enrollment and privacy bridge methods (docs/UI_ROUTE_B_WEBVIEW2.md §7.2).</summary>
public sealed partial class WebUiBridge
{
    private readonly List<AudioUtterance> _enrollmentUtterances = new();
    private EnrollmentRecordingSession? _enrollmentSession;

    public string ListSpeakerUsers()
    {
        var users = SettingsMetadataProvider.ListSpeakerUsers(_context.Enrollment)
            .Select(u => new { id = u.Id, displayName = u.DisplayName, isNone = u.IsNone });
        return Serialize(users);
    }

    public string StartEnrollmentUtterance()
    {
        return RunOnUiForJson(() =>
        {
            if (_context.EnrollmentCapture is null)
            {
                return Serialize(new { ok = false, error = "未配置录音采集源。" });
            }

            if (_enrollmentSession is not null)
            {
                return Serialize(new { ok = false, error = "已有进行中的录音。" });
            }

            try
            {
                _enrollmentSession = _context.EnrollmentCapture.StartRecording();
                return Serialize(new { ok = true });
            }
            catch (Exception ex)
            {
                return Serialize(new { ok = false, error = ex.Message });
            }
        });
    }

    public string StopEnrollmentUtterance()
    {
        return RunOnUiForJson(() =>
        {
            if (_enrollmentSession is null)
            {
                return Serialize(new
                {
                    durationMs = 0,
                    ok = false,
                    message = "没有进行中的录音。",
                    utteranceCount = _enrollmentUtterances.Count,
                });
            }

            var utterance = _enrollmentSession.Stop();
            _enrollmentSession.Dispose();
            _enrollmentSession = null;

            if (utterance is null || utterance.Duration < TimeSpan.FromSeconds(1))
            {
                return Serialize(new
                {
                    durationMs = (int)(utterance?.Duration.TotalMilliseconds ?? 0),
                    ok = false,
                    message = "录音太短，请录 3~5 秒。",
                    utteranceCount = _enrollmentUtterances.Count,
                });
            }

            _enrollmentUtterances.Add(utterance);
            return Serialize(new
            {
                durationMs = (int)utterance.Duration.TotalMilliseconds,
                ok = true,
                utteranceCount = _enrollmentUtterances.Count,
            });
        });
    }

    public string CompleteEnrollment(string name, int utteranceCount)
    {
        return RunOnUiForJson(() =>
        {
            if (_context.Enrollment is null)
            {
                return Serialize(new { ok = false, error = "说话人服务未加载。" });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Serialize(new { ok = false, error = "请输入姓名。" });
            }

            if (_enrollmentUtterances.Count < 3)
            {
                return Serialize(new { ok = false, error = "请至少完成 3 段录音。" });
            }

            if (utteranceCount > 0 && utteranceCount != _enrollmentUtterances.Count)
            {
                // Web page count is advisory; server-side list is authoritative.
            }

            try
            {
                var userId = _context.Enrollment.AddUser(name.Trim(), _enrollmentUtterances);
                _context.Settings.CurrentSpeakerUserId = userId;
                _context.SettingsStore.Save(_context.Settings);
                _enrollmentUtterances.Clear();
                return Serialize(new { ok = true, userId });
            }
            catch (Exception ex)
            {
                return Serialize(new { ok = false, error = ex.Message });
            }
        });
    }

    public string GetPrivacyConsentState(string apiBaseUrl)
    {
        if (!PrivacyConfirmation.TryResolveHost(apiBaseUrl, out var host))
        {
            return Serialize(new
            {
                needsPrompt = false,
                host = string.Empty,
                apiBaseUrl,
                isLoopback = false,
                message = string.Empty,
            });
        }

        var isLoopback = PrivacyConfirmation.IsLoopbackHost(host);
        var needsPrompt = !isLoopback
            && PrivacyConfirmation.ShouldPromptForHost(apiBaseUrl, _context.Settings.PrivacyAcceptedHost);

        var message = needsPrompt
            ? $"提示词整理将把识别文本发送到 {host}。继续？"
            : string.Empty;

        return Serialize(new
        {
            needsPrompt,
            host,
            apiBaseUrl,
            isLoopback,
            message,
        });
    }

    public string AcceptPrivacy(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Serialize(new { ok = false });
        }

        _context.Settings.PrivacyAcceptedHost = host.Trim();
        _context.SettingsStore.Save(_context.Settings);
        return Serialize(new { ok = true });
    }

    private string RunOnUiForJson(Func<string> action)
    {
        if (_context.HostForm is { IsDisposed: false } form && form.InvokeRequired)
        {
            return (string)form.Invoke(action)!;
        }

        return action();
    }
}
