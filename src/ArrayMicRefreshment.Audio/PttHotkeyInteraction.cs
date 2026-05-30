using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>Shared press/toggle decision for global PTT hotkey hosts.</summary>
public static class PttHotkeyInteraction
{
    /// <summary>
    /// Handles one hotkey activation (RegisterHotKey WM_HOTKEY or validated chord down).
    /// Returns whether <paramref name="raisePressed"/> or <paramref name="raiseReleased"/> was invoked.
    /// </summary>
    public static bool OnHotkeyActivation(
        PttRecordingMode mode,
        ref bool sessionActive,
        Action raisePressed,
        Action raiseReleased)
    {
        if (mode == PttRecordingMode.Toggle)
        {
            if (sessionActive)
            {
                sessionActive = false;
                raiseReleased();
                return true;
            }

            sessionActive = true;
            raisePressed();
            return true;
        }

        if (sessionActive)
        {
            return false;
        }

        sessionActive = true;
        raisePressed();
        return true;
    }
}
