#if WINDOWS
using Microsoft.Win32;
using Serilog;

namespace ArrayMicRefreshment.App;

internal static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ArrayMicRefreshment";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            Log.Warning("Launch-at-startup registry key not found: HKCU\\{Path}", RunKeyPath);
            return;
        }

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            Log.Information("Removed launch-at-startup registry entry");
            return;
        }

        var exePath = ResolveExecutablePath();
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Log.Warning("Launch-at-startup skipped: executable path unavailable");
            return;
        }

        key.SetValue(ValueName, Quote(exePath));
        Log.Information("Registered launch-at-startup: {Path}", exePath);
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    private static string? ResolveExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Application.ExecutablePath;
        }

        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    }

    private static string Quote(string path) => path.Contains(' ') ? $"\"{path}\"" : path;
}
#endif
