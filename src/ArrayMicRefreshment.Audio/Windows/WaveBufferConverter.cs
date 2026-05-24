#if WINDOWS
using NAudio.Wave;

namespace ArrayMicRefreshment.Audio;

/// <summary>Convert NAudio capture buffers to 16-bit PCM LE for the pipeline.</summary>
public static class WaveBufferConverter
{
    public static byte[] ToPcm16Le(ReadOnlySpan<byte> buffer, int bytesRecorded, WaveFormat format)
    {
        if (bytesRecorded <= 0)
        {
            return Array.Empty<byte>();
        }

        return format.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat => IeeeFloatToPcm16Le(buffer.Slice(0, bytesRecorded), format.Channels),
            WaveFormatEncoding.Pcm when format.BitsPerSample == 16 =>
                buffer.Slice(0, bytesRecorded).ToArray(),
            _ => throw new NotSupportedException(
                $"不支持的录音格式: {format.Encoding}, {format.BitsPerSample}bit, {format.Channels}ch。"),
        };
    }

    private static byte[] IeeeFloatToPcm16Le(ReadOnlySpan<byte> ieeeFloat, int channels)
    {
        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        var frameCount = ieeeFloat.Length / (4 * channels);
        if (frameCount == 0)
        {
            return Array.Empty<byte>();
        }

        var pcm = new byte[frameCount * channels * 2];
        for (var i = 0; i < frameCount; i++)
        {
            for (var ch = 0; ch < channels; ch++)
            {
                var offset = (i * channels + ch) * 4;
                var sample = BitConverter.ToSingle(ieeeFloat.Slice(offset, 4));
                sample = Math.Clamp(sample, -1f, 1f);
                var s = (short)Math.Round(sample * 32767f);
                BitConverter.TryWriteBytes(pcm.AsSpan((i * channels + ch) * 2, 2), s);
            }
        }

        return pcm;
    }
}
#endif
