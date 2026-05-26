using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.App.Web;

/// <summary>JS ↔ C# bridge contract for WebView2 host pages (see docs/UI_ROUTE_B_WEBVIEW2.md §7.2).</summary>
[ComVisible(true)]
public interface IWebUiBridge
{
    // Metadata / read-only
    string GetAppInfo();

    string GetRuntimeState();

    string ListAudioDevices();

    string ListSpeakerUsers();

    string ListAsrModels();

    string ListOptionalOverlaySkills();

    string GetSkillsCatalogStatus();

    // Settings read/write
    string LoadSettingsDraft();

    string ValidateSettingsDraft(string draftJson);

    string SaveSettingsDraft(string draftJson);

    string TestLlmConnection(string? draftJson);

    // Hotkey capture (native modal)
    string OpenHotkeyCaptureDialog(string currentHotkey);

    // Enrollment
    string StartEnrollmentUtterance();

    string StopEnrollmentUtterance();

    string CompleteEnrollment(string name, int utteranceCount);

    // Privacy
    string GetPrivacyConsentState(string apiBaseUrl);

    string AcceptPrivacy(string host);

    void RequestClose(bool success);
}
