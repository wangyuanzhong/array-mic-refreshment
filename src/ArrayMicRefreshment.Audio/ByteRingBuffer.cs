namespace ArrayMicRefreshment.Audio;

/// <summary>Fixed-size ring buffer for streaming PCM bytes.</summary>
public sealed class ByteRingBuffer
{
    private readonly byte[] _buffer;
    private int _write;
    private int _count;

    public ByteRingBuffer(int capacityBytes)
    {
        _buffer = new byte[capacityBytes];
    }

    public int Count => _count;

    public int Capacity => _buffer.Length;

    public void Write(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            _buffer[_write] = b;
            _write = (_write + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }
        }
    }

    public byte[] Snapshot()
    {
        if (_count == 0)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[_count];
        var start = (_write - _count + _buffer.Length) % _buffer.Length;
        if (start + _count <= _buffer.Length)
        {
            _buffer.AsSpan(start, _count).CopyTo(result);
        }
        else
        {
            var first = _buffer.Length - start;
            _buffer.AsSpan(start, first).CopyTo(result);
            _buffer.AsSpan(0, _count - first).CopyTo(result.AsSpan(first));
        }

        return result;
    }

    public void Clear()
    {
        _write = 0;
        _count = 0;
    }
}
