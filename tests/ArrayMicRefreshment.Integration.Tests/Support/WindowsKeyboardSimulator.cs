#if WINDOWS

using System.Runtime.InteropServices;
using ArrayMicRefreshment.Audio;

namespace ArrayMicRefreshment.Integration.Tests.Support;

/// <summary>Synthetic chord input for PTT black-box tests (does not use RegisterHotKey).</summary>
internal static class WindowsKeyboardSimulator
{
    private const int InputKeyboard = 1;
    private const uint KeyeventfKeyup = 0x0002;

    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkMenu = 0x12;
    private const ushort VkLWin = 0x5B;

    /// <summary>False when the session cannot inject keys (headless CI, no interactive desktop).</summary>
    public static bool CanSimulateInput()
    {
        try
        {
            var input = new INPUT
            {
                Type = InputKeyboard,
                Union = new InputUnion
                {
                    Keyboard = new KEYBDINPUT
                    {
                        Vk = 0xFF,
                        Flags = KeyeventfKeyup,
                    },
                },
            };

            var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            return sent == 1;
        }
        catch
        {
            return false;
        }
    }

    public static void HoldChord(bool ctrl, bool shift, bool alt, bool win, ushort vk)
    {
        if (ctrl)
        {
            KeyDown(VkControl);
        }

        if (shift)
        {
            KeyDown(VkShift);
        }

        if (alt)
        {
            KeyDown(VkMenu);
        }

        if (win)
        {
            KeyDown(VkLWin);
        }

        Thread.Sleep(25);
        KeyDown(vk);
    }

    public static void ReleaseChord(bool ctrl, bool shift, bool alt, bool win, ushort vk)
    {
        KeyUp(vk);
        Thread.Sleep(25);

        if (win)
        {
            KeyUp(VkLWin);
        }

        if (alt)
        {
            KeyUp(VkMenu);
        }

        if (shift)
        {
            KeyUp(VkShift);
        }

        if (ctrl)
        {
            KeyUp(VkControl);
        }
    }

    public static void TapChord(bool ctrl, bool shift, bool alt, bool win, ushort vk, int holdMs = 120)
    {
        HoldChord(ctrl, shift, alt, win, vk);
        Thread.Sleep(holdMs);
        ReleaseChord(ctrl, shift, alt, win, vk);
    }

    /// <summary>Simulate any hotkey string accepted by <see cref="HotkeyParser"/>.</summary>
    public static void TapHotkeyExpression(string hotkeyExpression, int holdMs = 120)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out var error))
        {
            throw new ArgumentException(error ?? "Invalid hotkey", nameof(hotkeyExpression));
        }

        TapChord(chord!.Ctrl, chord.Shift, chord.Alt, chord.Win, (ushort)chord.VirtualKey, holdMs);
    }

    public static void HoldChordForExpression(string hotkeyExpression, int holdMs = 0)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out var error))
        {
            throw new ArgumentException(error ?? "Invalid hotkey", nameof(hotkeyExpression));
        }

        HoldChord(chord!.Ctrl, chord.Shift, chord.Alt, chord.Win, (ushort)chord.VirtualKey);
        if (holdMs > 0)
        {
            Thread.Sleep(holdMs);
        }
    }

    public static void ReleaseHotkeyExpression(string hotkeyExpression)
    {
        if (!HotkeyParser.TryParse(hotkeyExpression, out var chord, out var error))
        {
            throw new ArgumentException(error ?? "Invalid hotkey", nameof(hotkeyExpression));
        }

        ReleaseChord(chord!.Ctrl, chord.Shift, chord.Alt, chord.Win, (ushort)chord.VirtualKey);
    }

    private static void KeyDown(ushort vk) => SendKey(vk, keyUp: false);

    private static void KeyUp(ushort vk) => SendKey(vk, keyUp: true);

    private static void SendKey(ushort vk, bool keyUp)
    {
        var input = new INPUT
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    Vk = vk,
                    Flags = keyUp ? KeyeventfKeyup : 0,
                },
            },
        };

        var sent = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        if (sent != 1)
        {
            throw new InvalidOperationException(
                $"SendInput failed for vk=0x{vk:X} keyUp={keyUp} (win32={Marshal.GetLastWin32Error()})");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}

#endif
