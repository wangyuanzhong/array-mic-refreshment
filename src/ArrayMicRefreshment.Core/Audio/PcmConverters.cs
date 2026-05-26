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

    /// <summary>Peak RMS over fixed windows (catches short speech buried in trailing silence).</summary>
    public static double ComputePeakWindowRms16Le(ReadOnlySpan<byte> pcm16LeMono, int windowBytes = 6400)
    {
        if (pcm16LeMono.Length < 2)
        {
            return 0;
        }

        windowBytes = Math.Max(320, windowBytes - (windowBytes % 2));
        var peak = 0.0;
        for (var offset = 0; offset < pcm16LeMono.Length; offset += windowBytes)
        {
            var len = Math.Min(windowBytes, pcm16LeMono.Length - offset);
            if (len < 2)
            {
                break;
            }

            var windowRms = ComputeRms16Le(pcm16LeMono.Slice(offset, len));
            if (windowRms > peak)
            {
                peak = windowRms;
            }
        }

        return peak;
    }

    /// <summary>Scale PCM so average RMS reaches <paramref name="targetRms"/> (normalized 0–1).</summary>
    public static byte[] NormalizeVolumeToTargetRms(ReadOnlySpan<byte> pcm16LeMono, double targetRms = 0.04)
    {
        if (pcm16LeMono.Length < 2)
        {
            return pcm16LeMono.ToArray();
        }

        var currentRms = ComputeRms16Le(pcm16LeMono);
        if (currentRms < 1e-6)
        {
            return pcm16LeMono.ToArray();
        }

        var scale = Math.Min(12.0, targetRms / currentRms);
        var sampleCount = pcm16LeMono.Length / 2;
        var result = new byte[pcm16LeMono.Length];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToInt16(pcm16LeMono.Slice(i * 2, 2));
            var scaled = (int)Math.Round(sample * scale);
            scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
            BitConverter.TryWriteBytes(result.AsSpan(i * 2, 2), (short)scaled);
        }

        return result;
    }

    /// <summary>Remove low-energy frames from the start and end (20 ms frames at 16 kHz).</summary>
    public static byte[] TrimSilenceEdges(ReadOnlySpan<byte> pcm16LeMono, double frameRmsThreshold = 0.0015, int frameBytes = 640)
    {
        if (pcm16LeMono.Length < frameBytes)
        {
            return pcm16LeMono.ToArray();
        }

        frameBytes -= frameBytes % 2;
        var start = 0;
        while (start + frameBytes <= pcm16LeMono.Length)
        {
            if (ComputeRms16Le(pcm16LeMono.Slice(start, frameBytes)) >= frameRmsThreshold)
            {
                break;
            }

            start += frameBytes;
        }

        var end = pcm16LeMono.Length;
        while (end - frameBytes >= start)
        {
            if (ComputeRms16Le(pcm16LeMono.Slice(end - frameBytes, frameBytes)) >= frameRmsThreshold)
            {
                break;
            }

            end -= frameBytes;
        }

        if (end <= start)
        {
            return Array.Empty<byte>();
        }

        return pcm16LeMono.Slice(start, end - start).ToArray();
    }
}
