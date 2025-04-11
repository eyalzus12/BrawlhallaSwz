using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz.Internal;

internal sealed class SubStream : Stream
{
    private Stream _stream;
    private long _position = 0;
    private long _length;

    public SubStream(Stream stream, long length)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _length = length;
    }

    public override bool CanRead => _stream?.CanRead ?? false;
    public override bool CanSeek => false; //_stream?.CanSeek ?? false;
    public override bool CanWrite => false;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        EnsureNotDisposed();
        int result = _stream.Read(buffer[..GetActualRead(buffer.Length)]);
        _position += result;
        return result;
    }

    public override int ReadByte()
    {
        EnsureNotDisposed();
        if (_position >= _length) return -1;
        int result = _stream.ReadByte();
        if (result != -1) _position++;
        return result;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        int result = await _stream.ReadAsync(buffer[..GetActualRead(buffer.Length)], cancellationToken);
        _position += result;
        return result;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        _length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        _stream = null!;
    }

    private int GetActualRead(int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;
        if (remaining < count) return (int)remaining;
        return count;
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_stream is null, this);
    }
}
