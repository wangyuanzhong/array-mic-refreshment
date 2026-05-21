namespace ArrayMicRefreshment.Core.Audio;

/// <summary>PCM helpers shared by ASR and speaker pipelines (16 kHz mono int16 LE).</summary>
public static class PcmConverters
{
    public const int TargetSampleRate = 16000;

    public static byte[] Ensure16KHzMonoPcm16Le(ReadOnlySpan<byte> pcm16LeMono, int sampleRate)
    {
        if (sampleRate == TargetSampleRate)
        {
            return pcm16LeMono.ToArray();
        }

        var floats = Pcm16LeToFloat(pcm16LeMono);
        var resampled = Resample(floats, sampleRate, TargetSampleRate);
        return FloatToPcm16Le(resampled);
    }

    public static float[] Pcm16LeToFloat(ReadOnlySpan<byte> pcm16LeMono)
    {
        if (pcm16LeMono.Length % 2 != 0)
        {
            throw new ArgumentException("PCM16 LE buffer length must be even.", nameof(pcm16LeMono));
        }

        var sampleCount = pcm16LeMono.Length / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(pcm16LeMono.Slice(i * 2, 2));
            samples[i] = sample / 32768f;
        }

        return samples;
    }

    public static byte[] FloatToPcm16Le(ReadOnlySpan<float> samples)
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

    public static float[] Resample(ReadOnlySpan<float> input, int inputRate, int outputRate)
    {
        if (inputRate == outputRate)
        {
            return input.ToArray();
        }

        if (input.Length == 0)
        {
            return Array.Empty<float>();
        }

        if (inputRate <= 0 || outputRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inputRate), "Sample rates must be positive.");
        }

        var outputLength = (int)Math.Max(1, Math.Round(input.Length * (double)outputRate / inputRate));
        var output = new float[outputLength];
        var ratio = (input.Length - 1) / (double)Math.Max(outputLength - 1, 1);

        for (var i = 0; i < outputLength; i++)
        {
            var srcIndex = i * ratio;
            var left = (int)Math.Floor(srcIndex);
            var right = Math.Min(left + 1, input.Length - 1);
            var frac = (float)(srcIndex - left);
            output[i] = input[left] * (1f - frac) + input[right] * frac;
        }

        return output;
    }
}
