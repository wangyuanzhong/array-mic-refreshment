namespace ArrayMicRefreshment.Core.Tests;

internal static class TestAudioHelper
{
    public static AudioUtterance CreateUtterance(double durationMs = 250, double frequencyHz = 440)
    {
        const int sampleRate = 16000;
        var sampleCount = (int)(sampleRate * durationMs / 1000.0);
        var pcm = new byte[sampleCount * 2];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(12000 * Math.Sin(2 * Math.PI * frequencyHz * i / sampleRate));
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2, 2), sample);
        }

        return new AudioUtterance
        {
            Pcm16LeMono = pcm,
            SampleRate = sampleRate,
            Duration = TimeSpan.FromMilliseconds(durationMs),
        };
    }
}
