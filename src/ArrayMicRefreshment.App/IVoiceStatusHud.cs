using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.App;

internal interface IVoiceStatusHud : IDisposable
{
    VoiceActivityPhase Phase { get; }

    IntPtr Handle { get; }

    void SetCorner(HudScreenCorner corner);

    void SetPhase(VoiceActivityPhase phase, string message);

    void HideSoon();
}
