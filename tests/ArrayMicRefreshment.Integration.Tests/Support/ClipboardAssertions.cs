namespace ArrayMicRefreshment.Integration.Tests.Support;

/// <summary>
/// Clipboard helpers that always run on a dedicated STA worker, so they keep
/// working even after <c>ConfigureAwait(false)</c> moves the test continuation
/// off of any STA-aware synchronization context.
/// </summary>
internal static class ClipboardAssertions
{
    public static string GetClipboardText() =>
        StaTestRunner.Run(() => System.Windows.Forms.Clipboard.GetText());

    public static void SetClipboardText(string text) =>
        StaTestRunner.Run(() =>
        {
            System.Windows.Forms.Clipboard.SetText(text);
            return 0;
        });
}
