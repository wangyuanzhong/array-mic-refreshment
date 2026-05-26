using System.Runtime.InteropServices;

namespace ArrayMicRefreshment.App;

internal static class TrayIconFactory
{
    private static readonly Dictionary<VoiceActivityPhase, Icon> Cache = new();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon ForPhase(VoiceActivityPhase phase)
    {
        if (!Cache.TryGetValue(phase, out var icon))
        {
            icon = CreateDotIcon(phase switch
            {
                VoiceActivityPhase.WakePrompt => Color.FromArgb(34, 197, 94),
                VoiceActivityPhase.Recording => Color.FromArgb(239, 68, 68),
                VoiceActivityPhase.Recognizing => Color.FromArgb(234, 179, 8),
                VoiceActivityPhase.Error => Color.FromArgb(220, 38, 38),
                _ => Color.FromArgb(107, 114, 128),
            });
            Cache[phase] = icon;
        }

        return icon;
    }

    private static Icon CreateDotIcon(Color color)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 4, 4, size - 8, size - 8);
        }

        return CloneIcon(bitmap);
    }

    private static Icon CloneIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
