namespace ArrayMicRefreshment.Audio;

/// <summary>Resample PCM to 16 kHz mono 16-bit little-endian.</summary>
public static class PcmResampler
{
    public const int TargetSampleRate = 16000;

    public static byte[] To16kHzMono16Le(
        ReadOnlySpan<byte> pcm,
        int sourceSampleRate,
        int channels,
        int bitsPerSample = 16)
    {
        if (sourceSampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceSampleRate));
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        if (bitsPerSample != 16)
        {
            throw new NotSupportedException("Only 16-bit PCM is supported in Phase 1.");
        }

        var frameCount = pcm.Length / (channels * 2);
        if (frameCount == 0)
        {
            return Array.Empty<byte>();
        }

        var mono = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
        {
            float sum = 0;
            for (var ch = 0; ch < channels; ch++)
            {
                var offset = (i * channels + ch) * 2;
                sum += BitConverter.ToInt16(pcm.Slice(offset, 2)) / 32768f;
            }

            mono[i] = sum / channels;
        }

        if (sourceSampleRate == TargetSampleRate)
        {
            return FloatMonoToPcm16Le(mono);
        }

        var ratio = (double)TargetSampleRate / sourceSampleRate;
        var outLen = Math.Max(1, (int)Math.Round(mono.Length * ratio));
        var resampled = new float[outLen];
        for (var i = 0; i < outLen; i++)
        {
            var srcPos = i / ratio;
            var idx = (int)srcPos;
            var frac = (float)(srcPos - idx);
            var s0 = mono[Math.Min(idx, mono.Length - 1)];
            var s1 = mono[Math.Min(idx + 1, mono.Length - 1)];
            resampled[i] = s0 + (s1 - s0) * frac;
        }

        return FloatMonoToPcm16Le(resampled);
    }

    public static double ComputeRms16Le(ReadOnlySpan<byte> pcm16LeMono)
    {
        if (pcm16LeMono.Length < 2)
        {
            return 0;
        }

        var samples = pcm16LeMono.Length / 2;
        double sumSq = 0;
        for (var i = 0; i < samples; i++)
        {
            var s = BitConverter.ToInt16(pcm16LeMono.Slice(i * 2, 2));
            var n = s / 32768.0;
            sumSq += n * n;
        }

        return Math.Sqrt(sumSq / samples);
    }

    private static byte[] FloatMonoToPcm16Le(ReadOnlySpan<float> samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var s = (short)Math.Round(clamped * 32767f);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2, 2), s);
        }

        return bytes;
    }

    /// <summary>Generate a sine wave PCM buffer for tests.</summary>
    public static byte[] GenerateSineWavePcm16(
        int sampleRate,
        int channels,
        double frequencyHz,
        TimeSpan duration,
        double amplitude = 0.5)
    {
        var sampleCount = (int)(sampleRate * duration.TotalSeconds);
        var bytes = new byte[sampleCount * channels * 2];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRate;
            var value = (short)Math.Round(Math.Sin(2 * Math.PI * frequencyHz * t) * amplitude * 32767);
            for (var ch = 0; ch < channels; ch++)
            {
                var offset = (i * channels + ch) * 2;
                BitConverter.TryWriteBytes(bytes.AsSpan(offset, 2), value);
            }
        }

        return bytes;
    }
}
