using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Integration.Tests.Support;

internal static class AudioTestResources
{
    public static AudioUtterance CreateShortUtterance()
    {
        var wavPath = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "short.wav");

        if (File.Exists(wavPath))
        {
            return LoadWavUtterance(wavPath);
        }

        return SynthesizeToneUtterance(durationSeconds: 0.5, frequencyHz: 440);
    }

    public static string? TryFindRealSenseVoiceModelDirectory(string modelsDirectory)
    {
        var root = Path.IsPathRooted(modelsDirectory)
            ? modelsDirectory
            : Path.GetFullPath(Path.Combine(RepoRoot.Find(), modelsDirectory));
        var primary = Path.Combine(root, SenseVoiceModelResolver.PrimaryModelId);
        return Directory.Exists(primary) ? primary : null;
    }

    private static AudioUtterance SynthesizeToneUtterance(double durationSeconds, double frequencyHz)
    {
        const int sampleRate = 16000;
        var sampleCount = (int)(sampleRate * durationSeconds);
        var pcm = new byte[sampleCount * 2];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = i / (double)sampleRate;
            var envelope = Math.Min(1.0, t * 20) * Math.Min(1.0, (durationSeconds - t) * 20);
            var sample = (short)(short.MaxValue * 0.25 * envelope * Math.Sin(2 * Math.PI * frequencyHz * t));
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return new AudioUtterance
        {
            Pcm16LeMono = pcm,
            SampleRate = sampleRate,
            Duration = TimeSpan.FromSeconds(durationSeconds),
        };
    }

    private static AudioUtterance LoadWavUtterance(string path)
    {
        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);
        _ = reader.ReadChars(4);
        _ = reader.ReadInt32();
        _ = reader.ReadChars(4);
        _ = reader.ReadChars(4);
        _ = reader.ReadInt32();
        _ = reader.ReadInt16();
        _ = reader.ReadInt16();
        var sampleRate = reader.ReadInt32();
        _ = reader.ReadInt32();
        _ = reader.ReadInt16();
        _ = reader.ReadInt16();
        _ = reader.ReadChars(4);
        var dataSize = reader.ReadInt32();
        var pcm = reader.ReadBytes(dataSize);
        var duration = TimeSpan.FromSeconds((double)pcm.Length / (2 * sampleRate));
        return new AudioUtterance
        {
            Pcm16LeMono = pcm,
            SampleRate = sampleRate,
            Duration = duration,
        };
    }
}

internal static class RepoRoot
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ArrayMicRefreshment.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
