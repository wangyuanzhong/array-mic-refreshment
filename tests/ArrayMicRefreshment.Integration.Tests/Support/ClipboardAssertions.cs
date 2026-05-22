namespace ArrayMicRefreshment.Integration.Tests.Support;

internal static class ClipboardAssertions
{
    public static string GetClipboardText() =>
        System.Windows.Forms.Clipboard.GetText();

    public static void SetClipboardText(string text) =>
        System.Windows.Forms.Clipboard.SetText(text);
}
