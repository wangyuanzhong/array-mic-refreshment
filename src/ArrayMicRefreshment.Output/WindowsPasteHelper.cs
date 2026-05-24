#if WINDOWS
using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.Output;

public static class WindowsPasteHelper
{
    private const int SwRestore = 9;

    public static (IntPtr Root, IntPtr Focus) CaptureForeground()
    {
        var root = GetForegroundWindow();
        if (root == IntPtr.Zero)
        {
            return (IntPtr.Zero, IntPtr.Zero);
        }

        return (root, ResolveFocusHwnd(root));
    }

    public static IntPtr ResolveFocusHwnd(IntPtr rootWindow)
    {
        if (rootWindow == IntPtr.Zero || !IsWindow(rootWindow))
        {
            return IntPtr.Zero;
        }

        var threadId = GetWindowThreadProcessId(rootWindow, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != IntPtr.Zero && IsWindow(info.hwndFocus))
        {
            return info.hwndFocus;
        }

        return rootWindow;
    }

    private const int SwShowMinimized = 2;

    public static bool TryActivateForPaste(IntPtr rootWindow, IntPtr focusWindow)
    {
        if (rootWindow == IntPtr.Zero || !IsWindow(rootWindow))
        {
            return false;
        }

        // If already foreground, just set focus without changing window state
        if (GetForegroundWindow() == rootWindow)
        {
            var currentFocus = focusWindow != IntPtr.Zero && IsWindow(focusWindow) ? focusWindow : ResolveFocusHwnd(rootWindow);
            if (currentFocus != IntPtr.Zero)
            {
                _ = SetFocus(currentFocus);
            }
            return true;
        }

        _ = GetWindowThreadProcessId(rootWindow, out var processId);
        if (processId != 0)
        {
            _ = AllowSetForegroundWindow((int)processId);
        }

        _ = AllowSetForegroundWindow(-1);

        // Only restore if minimized, never change size of normal/maximized windows
        if (IsIconic(rootWindow))
        {
            _ = ShowWindow(rootWindow, SwRestore);
        }

        _ = BringWindowToTop(rootWindow);
        if (!SetForegroundWindow(rootWindow))
        {
            return false;
        }

        var focus = focusWindow != IntPtr.Zero && IsWindow(focusWindow) ? focusWindow : ResolveFocusHwnd(rootWindow);
        if (focus != IntPtr.Zero)
        {
            _ = SetFocus(focus);
        }

        return true;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgti);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
#endif
