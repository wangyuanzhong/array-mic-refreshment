namespace ArrayMicRefreshment.Integration.Tests;

internal static class WindowsTestGuards
{
    public static void SkipUnlessWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new SkipTestException("Requires Windows (integration tests are windows-only).");
        }
    }
}

/// <summary>Maps to xUnit skipped test (not a failure).</summary>
internal sealed class SkipTestException : Exception
{
    public SkipTestException(string message)
        : base(message)
    {
    }
}
