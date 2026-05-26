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

    public byte[] Snapshot() => SnapshotLast(_count);

    public byte[] SnapshotLast(int maxBytes)
    {
        if (_count == 0 || maxBytes <= 0)
        {
            return Array.Empty<byte>();
        }

        var take = Math.Min(maxBytes, _count);
        var result = new byte[take];
        var start = (_write - take + _buffer.Length) % _buffer.Length;
        if (start + take <= _buffer.Length)
        {
            _buffer.AsSpan(start, take).CopyTo(result);
        }
        else
        {
            var first = _buffer.Length - start;
            _buffer.AsSpan(start, first).CopyTo(result);
            _buffer.AsSpan(0, take - first).CopyTo(result.AsSpan(first));
        }

        return result;
    }

    public void Clear()
    {
        _write = 0;
        _count = 0;
    }
}
