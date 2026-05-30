#if WINDOWS

using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Audio;

/// <summary>Global PTT via low-level keyboard hook; release detected by UI-thread key polling.</summary>
public sealed class NAudioPushToTalkSource : IPushToTalkSource, IDisposable
{
    private readonly IGlobalHotkeyHost _host;
    private readonly bool _ownsHost;
    private HotkeyChord? _chord;

    public NAudioPushToTalkSource(string hotkeyExpression)
        : this(hotkeyExpression, null, registerImmediately: true)
    {
    }

    public NAudioPushToTalkSource(string hotkeyExpression, bool registerImmediately)
        : this(hotkeyExpression, null, registerImmediately)
    {
    }

    public NAudioPushToTalkSource(string hotkeyExpression, IGlobalHotkeyHost? host)
        : this(hotkeyExpression, host, registerImmediately: true)
    {
    }

    public NAudioPushToTalkSource(string hotkeyExpression, IGlobalHotkeyHost? host, bool registerImmediately)
    {
        if (host is null)
        {
            _host = new LowLevelHotkeyHost();
            _ownsHost = true;
        }
        else
        {
            _host = host;
            _ownsHost = false;
        }

        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out _))
        {
            chord = new HotkeyChord { Ctrl = true, Alt = true, VirtualKey = 0x20 };
        }

        _chord = chord;
        HotkeyDisplay = chord!.ToString();
        _host.ForegroundAtPress += hwnd => ForegroundAtPress?.Invoke(hwnd);
        _host.ForegroundAtRelease += hwnd => ForegroundAtRelease?.Invoke(hwnd);
        _host.HotkeyPressed += (_, _) => PttPressed?.Invoke(this, EventArgs.Empty);
        _host.HotkeyReleased += (_, _) =>
        {
            _releaseCount++;
            PttReleased?.Invoke(this, EventArgs.Empty);
        };

        if (registerImmediately && chord is not null)
        {
            if (!_host.TryRegister(chord))
            {
                Serilog.Log.Warning("PTT hotkey hook failed for {Hotkey}", hotkeyExpression);
            }
        }
    }

    public event EventHandler? PttPressed;
    public event EventHandler? PttReleased;
    public event Action<IntPtr>? ForegroundAtPress;
    public event Action<IntPtr>? ForegroundAtRelease;

    public string HotkeyDisplay { get; private set; }

    public bool IsRegistered => _host.IsRegistered;

    private int _releaseCount;

    public int ReleaseEventCount => _releaseCount;

    public bool TryUpdateHotkey(string hotkeyExpression, out string? error)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out error))
        {
            return false;
        }

        _host.Unregister();
        if (!_host.TryRegister(chord!))
        {
            error = "热键无法启用（全局键盘钩子安装失败）。请重启应用或以管理员身份运行后重试。";
            if (_chord is not null)
            {
                _host.TryRegister(_chord);
            }

            return false;
        }

        _chord = chord;
        HotkeyDisplay = chord!.ToString();
        return true;
    }

    public void UpdateHotkey(string hotkeyExpression) => TryUpdateHotkey(hotkeyExpression, out _);

    public void SimulatePress() => PttPressed?.Invoke(this, EventArgs.Empty);

    public void SimulateRelease()
    {
        _host.ResetHeldState();

        _releaseCount++;
        PttReleased?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _host.Unregister();
        if (_ownsHost)
        {
            _host.Dispose();
        }
    }
}

#endif
