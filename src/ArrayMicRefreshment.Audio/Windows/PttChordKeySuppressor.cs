#if WINDOWS

using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;

namespace ArrayMicRefreshment.Audio;

/// <summary>
/// WH_KEYBOARD_LL companion for <see cref="GlobalHotkeyListener"/>:
/// while PTT is active, swallows the chord main key so letter/space keys do not leak into the focused app.
/// Hook is installed only during an active PTT session (no global overhead while idle).
/// </summary>
internal sealed class PttChordKeySuppressor : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSysKeydown = 0x0104;
    private const int WmSysKeyup = 0x0105;

    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private HotkeyChord? _chord;
    private bool _pttActive;

    public PttChordKeySuppressor()
    {
        _hookProc = OnHook;
    }

    /// <summary>Fired on the hook thread when the main key is released during an active PTT session.</summary>
    public event Action? MainKeyReleased;

    public void Register(HotkeyChord chord)
    {
        _chord = chord;
    }

    public void Unregister()
    {
        _chord = null;
        SetPttActive(false);
    }

    public void SetPttActive(bool active)
    {
        if (_chord is null)
        {
            return;
        }

        if (active)
        {
            if (!_pttActive)
            {
                _pttActive = true;
                EnsureHookInstalled();
            }

            return;
        }

        if (!_pttActive)
        {
            return;
        }

        _pttActive = false;
        UninstallHook();
        NativeKeyboardInput.SendKeyUp((ushort)_chord.VirtualKey);
        Log.Debug("PTT chord main key 0x{Vk:X} key-up flushed after release", _chord.VirtualKey);
    }

    public void Dispose() => Unregister();

    private bool EnsureHookInstalled()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return true;
        }

        using var process = Process.GetCurrentProcess();
        var moduleName = process.MainModule?.ModuleName;
        var moduleHandle = !string.IsNullOrEmpty(moduleName)
            ? GetModuleHandle(moduleName)
            : GetModuleHandle(null);
        if (moduleHandle == IntPtr.Zero)
        {
            moduleHandle = GetModuleHandle(null);
        }

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            Log.Warning("PTT chord key suppressor hook install failed (win32={Err})", Marshal.GetLastWin32Error());
        }

        return _hookHandle != IntPtr.Zero;
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr OnHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _pttActive && _chord is not null)
        {
            var msg = wParam.ToInt32();
            var keyDown = msg is WmKeydown or WmSysKeydown;
            var keyUp = msg is WmKeyup or WmSysKeyup;
            if (keyDown || keyUp)
            {
                var data = Marshal.PtrToStructure<KbdllHookStruct>(lParam);
                if (HotkeyChordKeys.IsMainKey(_chord, data.VirtualKey))
                {
                    if (keyUp)
                    {
                        try
                        {
                            MainKeyReleased?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "PTT suppressor MainKeyReleased handler failed");
                        }
                    }

                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdllHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}

#endif
