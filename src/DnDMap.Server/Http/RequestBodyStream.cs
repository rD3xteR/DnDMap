namespace DnDMap.Server.Http;

/// <summary>
/// Тело запроса: сначала отдаёт байты, уже прочитанные вместе с заголовками,
/// затем читает из сети, но не больше Content-Length.
/// </summary>
internal sealed class RequestBodyStream : Stream
{
    private readonly Stream _inner;
    private readonly byte[] _prefix;
    private int _prefixOffset;
    private long _remaining;

    public RequestBodyStream(byte[] prefix, Stream inner, long length)
    {
        _inner = inner;
        _prefix = prefix;
        _remaining = length;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(new Span<byte>(buffer, offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_remaining <= 0 || buffer.Length == 0)
            return 0;

        var take = (int)Math.Min(buffer.Length, _remaining);

        if (_prefixOffset < _prefix.Length)
        {
            var available = Math.Min(take, _prefix.Length - _prefixOffset);
            _prefix.AsSpan(_prefixOffset, available).CopyTo(buffer);
            _prefixOffset += available;
            _remaining -= available;
            return available;
        }

        var read = _inner.Read(buffer[..take]);
        _remaining -= read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_remaining <= 0 || buffer.Length == 0)
            return 0;

        var take = (int)Math.Min(buffer.Length, _remaining);

        if (_prefixOffset < _prefix.Length)
        {
            var available = Math.Min(take, _prefix.Length - _prefixOffset);
            _prefix.AsSpan(_prefixOffset, available).CopyTo(buffer.Span);
            _prefixOffset += available;
            _remaining -= available;
            return available;
        }

        var read = await _inner.ReadAsync(buffer[..take], cancellationToken);
        _remaining -= read;
        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
