using System.Diagnostics;
using System.Runtime.InteropServices;
using ArrayMicRefreshment.Core;
using Serilog;

namespace ArrayMicRefreshment.Output;

public sealed class ClipboardTranscriptSink : ITranscriptSink
{
    private readonly Func<nint>? _getSettingsWindowHandle;
    private readonly Func<IReadOnlyList<nint>>? _getExcludedWindowHandles;
    private readonly SynchronizationContext? _uiContext;
#if WINDOWS
    private nint _pasteRootWindow;
    private nint _pasteFocusWindow;
    private string _pendingPasteText = string.Empty;
#endif

    public event Action<string, bool>? Emitted;

    public ClipboardTranscriptSink(
        Func<nint>? getSettingsWindowHandle = null,
        SynchronizationContext? uiContext = null,
        Func<IReadOnlyList<nint>>? getExcludedWindowHandles = null)
    {
        _getSettingsWindowHandle = getSettingsWindowHandle;
        _uiContext = uiContext;
        _getExcludedWindowHandles = getExcludedWindowHandles;
    }

    public void SetPasteTarget(IntPtr rootHwnd, IntPtr focusHwnd = default)
    {
#if WINDOWS
        var excluded = GetExcludedWindowHandles();
        if (rootHwnd == IntPtr.Zero || !IsWindow(rootHwnd))
        {
            Log.Debug("SetPasteTarget skipped: invalid root hwnd {Root}", rootHwnd);
            return;
        }

        if (IsPasteTargetExcluded(rootHwnd, excluded))
        {
            Log.Debug("SetPasteTarget skipped: excluded root window {Root}", rootHwnd);
            return;
        }

        _pasteRootWindow = rootHwnd;
        _pasteFocusWindow = focusHwnd != IntPtr.Zero && IsWindow(focusHwnd) && !IsPasteTargetExcluded(focusHwnd, excluded)
            ? focusHwnd
            : WindowsPasteHelper.ResolveFocusHwnd(rootHwnd);
        Log.Debug("Paste target set root={Root} focus={Focus}", rootHwnd, _pasteFocusWindow);
#endif
    }

    public Task EmitAsync(string textToClipboard, bool pasteToCaret, CancellationToken cancellationToken)
    {
        Emitted?.Invoke(textToClipboard, pasteToCaret);
#if WINDOWS
        void SetClipboard()
        {
            Log.Debug("EmitAsync SetClipboard: paste={Paste} textLength={Len}", pasteToCaret, textToClipboard.Length);
            _pendingPasteText = textToClipboard;
            SetClipboardWithRetry(textToClipboard);
            if (!pasteToCaret)
            {
                Log.Debug("Paste skipped: pasteToCaret=false");
                return;
            }

            SchedulePasteOnUiThread();
        }

        if (_uiContext is not null)
        {
            _uiContext.Post(_ => SetClipboard(), null);
        }
        else
        {
            RunOnSta(SetClipboard);
        }
#else
        _ = cancellationToken;
        Console.WriteLine($"[clipboard-fallback] paste={pasteToCaret} text={textToClipboard}");
#endif
        return Task.CompletedTask;
    }

#if WINDOWS
    private void SchedulePasteOnUiThread()
    {
        if (_uiContext is not null)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                Log.Debug("WinForms timer callback executing paste");
                TryPasteToCaret();
            };
            timer.Start();
            Log.Debug("Paste WinForms.Timer started (500ms)");
            return;
        }

        RunOnSta(() =>
        {
            Thread.Sleep(500);
            TryPasteToCaret();
        });
    }

    private void TryPasteToCaret()
    {
        var text = _pendingPasteText;
        Log.Debug("TryPasteToCaret called, textLength={Len}", text?.Length ?? 0);
        if (string.IsNullOrEmpty(text))
        {
            Log.Warning("Paste aborted: empty text");
            return;
        }

        var root = _pasteRootWindow;
        var focus = _pasteFocusWindow;
        Log.Debug("Initial paste target: root={Root:X} focus={Focus:X}", root, focus);

        if (root == IntPtr.Zero || !IsWindow(root))
        {
            Log.Debug("Recorded root invalid, capturing foreground window");
            (root, focus) = WindowsPasteHelper.CaptureForegroundExcluding(GetExcludedWindowHandles());
            Log.Debug("Captured foreground: root={Root:X} focus={Focus:X}", root, focus);
        }

        if (IsPasteTargetExcluded(root, GetExcludedWindowHandles()))
        {
            Log.Warning("Paste skipped: foreground resolved to excluded window {Root:X}", root);
            return;
        }

        if (focus == IntPtr.Zero || !IsWindow(focus))
        {
            focus = WindowsPasteHelper.ResolveFocusHwnd(root);
            Log.Debug("Resolved focus: {Focus:X}", focus);
        }

        var settingsHandle = _getSettingsWindowHandle?.Invoke() ?? IntPtr.Zero;
        if (root == IntPtr.Zero || IsPasteTargetExcluded(root, GetExcludedWindowHandles()))
        {
            Log.Warning("Paste skipped: no valid target window (root={Root:X} settings={Settings:X})", root, settingsHandle);
            return;
        }

        // Log modifier key states before release
        LogModifierStates("Before release");

        // Release modifiers with retry loop
        bool modifiersReleased = EnsureModifiersReleased();
        Log.Debug("Modifiers released: {Released}", modifiersReleased);

        Thread.Sleep(200);

        // Try paste up to 3 times with increasing delays
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Log.Debug("Paste attempt {Attempt}/3", attempt);

            // Activate window on each attempt (window may have changed)
            if (!WindowsPasteHelper.TryActivateForPaste(root, focus))
            {
                Log.Warning("Paste attempt {Attempt}: could not activate window {Root:X}", attempt, root);
                Thread.Sleep(200);
                continue;
            }

            Log.Debug("Window activated: root={Root:X} focus={Focus:X}", root, focus);

            if (TrySendCtrlV())
            {
                Log.Information("Paste succeeded on attempt {Attempt} via SendKeys/SendInput", attempt);
                return;
            }

            Log.Warning("Paste attempt {Attempt}: SendKeys/SendInput failed", attempt);

            if (focus != IntPtr.Zero && TrySendWmPaste(focus))
            {
                Log.Information("Paste succeeded on attempt {Attempt} via WM_PASTE", attempt);
                return;
            }

            Log.Warning("Paste attempt {Attempt}: WM_PASTE also failed", attempt);

            if (focus != IntPtr.Zero && TryPostMessagePaste(focus))
            {
                Log.Information("Paste succeeded on attempt {Attempt} via PostMessage", attempt);
                return;
            }

            Log.Warning("Paste attempt {Attempt}: PostMessage also failed", attempt);

            if (attempt < 3)
            {
                int delay = attempt * 300;
                Log.Debug("Waiting {Delay}ms before retry...", delay);
                Thread.Sleep(delay);
                EnsureModifiersReleased();
            }
        }

        Log.Error("Paste failed after 3 attempts for root={Root:X} focus={Focus:X}", root, focus);
    }

    private IReadOnlyList<nint> GetExcludedWindowHandles()
        => _getExcludedWindowHandles?.Invoke() ?? Array.Empty<nint>();

    private static bool IsPasteTargetExcluded(nint hwnd, IReadOnlyList<nint> excluded)
        => WindowsPasteHelper.IsExcludedWindow(hwnd, excluded);

    private static void LogModifierStates(string context)
    {
        ushort[] modifierVks = [0xA2, 0xA3, 0x11, 0xA0, 0xA1, 0x10, 0xA4, 0xA5, 0x12, 0x5B, 0x5C, 0x20];
        string[] modifierNames = ["LCtrl", "RCtrl", "Ctrl", "LShift", "RShift", "Shift", "LAlt", "RAlt", "Alt", "LWin", "RWin", "Space"];
        var states = new System.Text.StringBuilder();
        for (int i = 0; i < modifierVks.Length; i++)
        {
            bool down = (GetAsyncKeyState(modifierVks[i]) & 0x8000) != 0;
            if (down) states.Append($"{modifierNames[i]} ");
        }
        Log.Debug("Modifier states ({Context}): {States}", context, states.Length > 0 ? states.ToString().Trim() : "(none)");
    }

    private static bool EnsureModifiersReleased()
    {
        ushort[] modifierVks = [0xA2, 0xA3, 0x11, 0xA0, 0xA1, 0x10, 0xA4, 0xA5, 0x12, 0x5B, 0x5C, 0x20];

        for (int round = 0; round < 5; round++)
        {
            bool anyDown = false;
            foreach (var vk in modifierVks)
            {
                if ((GetAsyncKeyState(vk) & 0x8000) != 0)
                {
                    anyDown = true;
                    var scan = (ushort)(MapVirtualKey(vk, 0) & 0xFFFF);
                    var input = new INPUT
                    {
                        Type = InputKeyboard,
                        Ki = new KEYBDINPUT
                        {
                            Vk = vk,
                            Scan = scan,
                            Flags = (ushort)((scan != 0 ? KeyeventfScanCode : 0) | KeyeventfKeyup),
                        },
                    };
                    _ = SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
                }
            }

            if (!anyDown)
            {
                Log.Debug("All modifiers released after {Rounds} round(s)", round + 1);
                return true;
            }

            Thread.Sleep(50);
        }

        Log.Warning("Modifiers may still be held after 5 release rounds");
        return false;
    }

    private static bool TrySendCtrlV()
    {
        try
        {
            System.Windows.Forms.SendKeys.SendWait("^v");
            Log.Debug("SendKeys.SendWait(\"^v\") succeeded");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("SendKeys Ctrl+V failed: {Error}, trying SendInput", ex.Message);
        }

        var ctrlScan = (ushort)(MapVirtualKey(VK_CONTROL, 0) & 0xFFFF);
        var vScan = (ushort)(MapVirtualKey(0x56, 0) & 0xFFFF);

        var inputs = new INPUT[4];
        inputs[0] = new INPUT
        {
            Type = InputKeyboard,
            Ki = new KEYBDINPUT
            {
                Vk = VK_CONTROL,
                Scan = ctrlScan,
                Flags = ctrlScan != 0 ? KeyeventfScanCode : 0,
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        };
        inputs[1] = new INPUT
        {
            Type = InputKeyboard,
            Ki = new KEYBDINPUT
            {
                Vk = 0x56,
                Scan = vScan,
                Flags = vScan != 0 ? KeyeventfScanCode : 0,
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        };
        inputs[2] = new INPUT
        {
            Type = InputKeyboard,
            Ki = new KEYBDINPUT
            {
                Vk = 0x56,
                Scan = vScan,
                Flags = (ushort)((vScan != 0 ? KeyeventfScanCode : 0) | KeyeventfKeyup),
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        };
        inputs[3] = new INPUT
        {
            Type = InputKeyboard,
            Ki = new KEYBDINPUT
            {
                Vk = VK_CONTROL,
                Scan = ctrlScan,
                Flags = (ushort)((ctrlScan != 0 ? KeyeventfScanCode : 0) | KeyeventfKeyup),
                Time = 0,
                ExtraInfo = IntPtr.Zero,
            },
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        Log.Debug("SendInput sent {Sent}/{Expected} inputs", sent, inputs.Length);
        return sent == inputs.Length;
    }

    private static bool TrySendWmPaste(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            return false;
        }

        try
        {
            var result = SendMessage(hwnd, WmPaste, IntPtr.Zero, IntPtr.Zero);
            Log.Debug("WM_PASTE sent to {Hwnd:X}, result={Result}", hwnd, result);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("WM_PASTE failed: {Error}", ex.Message);
            return false;
        }
    }

    private static bool TryPostMessagePaste(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            return false;
        }

        try
        {
            int vkCtrl = VK_CONTROL;
            int vkV = 0x56;
            int scanCtrl = (int)(MapVirtualKey((uint)vkCtrl, 0) & 0xFF);
            int scanV = (int)(MapVirtualKey((uint)vkV, 0) & 0xFF);

            // WM_KEYDOWN for Ctrl
            IntPtr lParamCtrlDown = (IntPtr)(1 | (scanCtrl << 16));
            PostMessage(hwnd, WmKeydown, (IntPtr)vkCtrl, lParamCtrlDown);

            // WM_KEYDOWN for V
            IntPtr lParamVDown = (IntPtr)(1 | (scanV << 16));
            PostMessage(hwnd, WmKeydown, (IntPtr)vkV, lParamVDown);

            // WM_KEYUP for V
            IntPtr lParamVUp = (IntPtr)(0xC0000000 | (scanV << 16));
            PostMessage(hwnd, WmKeyup, (IntPtr)vkV, lParamVUp);

            // WM_KEYUP for Ctrl
            IntPtr lParamCtrlUp = (IntPtr)(0xC0000000 | (scanCtrl << 16));
            PostMessage(hwnd, WmKeyup, (IntPtr)vkCtrl, lParamCtrlUp);

            Log.Debug("PostMessage key sequence sent to {Hwnd:X}", hwnd);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning("PostMessage paste failed: {Error}", ex.Message);
            return false;
        }
    }

    private void SetClipboardWithRetry(string text)
    {
        const int attempts = 5;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                System.Windows.Forms.Clipboard.SetText(text, TextDataFormat.UnicodeText);
                Log.Debug("Clipboard set successfully (attempt {Attempt})", i + 1);
                return;
            }
            catch (ExternalException) when (i < attempts - 1)
            {
                Log.Debug("Clipboard busy, retrying...");
                Thread.Sleep(40);
            }
        }
        Log.Warning("Failed to set clipboard after {Attempts} attempts", attempts);
    }

    private static void RunOnSta(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw error;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int InputKeyboard = 1;
    private const ushort VK_CONTROL = 0x11;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint KeyeventfScanCode = 0x0008;
    private const int WmPaste = 0x0302;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int Type;
        public KEYBDINPUT Ki;
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
#endif
}
