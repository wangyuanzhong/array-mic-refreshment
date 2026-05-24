namespace ArrayMicRefreshment.Speaker;

internal static class SpeakerEmbeddingMath
{
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0)
        {
            return 0f;
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0f;
        }

        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }

    public static float[] L2Normalize(ReadOnlySpan<float> vector)
    {
        var copy = vector.ToArray();
        L2NormalizeInPlace(copy);
        return copy;
    }

    public static void L2NormalizeInPlace(Span<float> vector)
    {
        double norm = 0;
        foreach (var v in vector)
        {
            norm += v * v;
        }

        if (norm <= 1e-12)
        {
            return;
        }

        var scale = (float)(1.0 / Math.Sqrt(norm));
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] *= scale;
        }
    }

    /// <summary>P1: Z-Normalize a score using cohort mean/std.</summary>
    public static float ZNormalize(float score, float cohortMean, float cohortStd)
    {
        if (cohortStd < 1e-6f)
        {
            return score > cohortMean ? 3.0f : -3.0f;
        }
        return (score - cohortMean) / cohortStd;
    }

    /// <summary>P1: Compute mean and std of a list of scores.</summary>
    public static (float Mean, float Std) ComputeMeanAndStd(ReadOnlySpan<float> scores)
    {
        if (scores.Length == 0)
        {
            return (0f, 1f);
        }

        double sum = 0;
        foreach (var s in scores)
        {
            sum += s;
        }
        var mean = (float)(sum / scores.Length);

        if (scores.Length == 1)
        {
            return (mean, 0.05f);
        }

        double sqDiffSum = 0;
        foreach (var s in scores)
        {
            var diff = s - mean;
            sqDiffSum += diff * diff;
        }
        var std = (float)Math.Sqrt(sqDiffSum / scores.Length);
        
        // Prevent division by near-zero
        if (std < 1e-6f)
        {
            std = 0.05f;
        }

        return (mean, std);
    }

    /// <summary>P1: Compute median of a list of scores.</summary>
    public static float Median(float[] scores)
    {
        if (scores.Length == 0)
        {
            return 0f;
        }

        var sorted = scores.OrderBy(s => s).ToArray();
        var mid = sorted.Length / 2;
        if (sorted.Length % 2 == 0)
        {
            return (sorted[mid - 1] + sorted[mid]) / 2.0f;
        }
        return sorted[mid];
    }

    /// <summary>P2: Compute RMS (Root Mean Square) of 16-bit PCM samples.</summary>
    public static double ComputeRms16Le(ReadOnlySpan<byte> pcm16LeMono)
    {
        if (pcm16LeMono.Length < 2)
        {
            return 0.0;
        }

        double sumSquares = 0;
        var sampleCount = pcm16LeMono.Length / 2;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(pcm16LeMono[i * 2] | (pcm16LeMono[i * 2 + 1] << 8));
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / sampleCount);
    }

    /// <summary>P2: Normalize audio volume to target RMS.</summary>
    public static byte[] NormalizeVolume(ReadOnlySpan<byte> pcm16LeMono, double targetRms = 8000.0)
    {
        var currentRms = ComputeRms16Le(pcm16LeMono);
        if (currentRms < 1.0)
        {
            return pcm16LeMono.ToArray();
        }

        var scale = targetRms / currentRms;
        // Clamp scale to avoid distortion
        scale = Math.Min(scale, 10.0);

        var result = new byte[pcm16LeMono.Length];
        var sampleCount = pcm16LeMono.Length / 2;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(pcm16LeMono[i * 2] | (pcm16LeMono[i * 2 + 1] << 8));
            var scaled = (int)(sample * scale);
            // Clamp to int16 range
            scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
            var newSample = (short)scaled;
            result[i * 2] = (byte)(newSample & 0xFF);
            result[i * 2 + 1] = (byte)((newSample >> 8) & 0xFF);
        }

        return result;
    }
}
