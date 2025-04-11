using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz;

public sealed class SwzEncryptStream : Stream
{
    private const int DEFAULT_BUFFER_SIZE = 8192;

    private Stream _stream;
    private readonly bool _leaveOpen;
    private SwzRandom _random;

    public uint Checksum { get; private set; } = 0;
    private int _index = -1;
    private byte[] _buffer = null!;

    public SwzEncryptStream(Stream stream, SwzRandom random, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(random);
        if (!stream.CanRead) throw new NotSupportedException("Given stream is not readable");

        _stream = stream;
        _leaveOpen = leaveOpen;
        _random = random;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => _stream?.CanWrite ?? false;
    public override long Length => _stream?.Length ?? 0;
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        EnsureNotDisposed();
        _stream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        EnsureNotDisposed();
        return _stream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureNotDisposed();
        // copy to internal buffer in chunks
        InitializeBuffer();
        InitializeChecksum();
        int copied = 0;
        while (copied < buffer.Length)
        {
            // copy
            int canCopy = Math.Min(_buffer.Length - copied, buffer.Length);
            buffer.Slice(copied, canCopy).CopyTo(_buffer);
            // encrypt section
            EncryptInternalBuffer(canCopy);
            // write
            _stream.Write(_buffer.AsSpan(0, canCopy));

            copied += canCopy;
        }
    }

    public override void WriteByte(byte value)
    {
        EnsureNotDisposed();
        InitializeChecksum();
        _stream.WriteByte(EncryptByte(value));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        // copy to internal buffer in chunks
        InitializeBuffer();
        InitializeChecksum();
        int copied = 0;
        while (copied < buffer.Length)
        {
            // copy
            int canCopy = Math.Min(_buffer.Length - copied, buffer.Length);
            buffer.Slice(copied, canCopy).CopyTo(_buffer);
            // encrypt section
            EncryptInternalBuffer(canCopy);
            // write
            await _stream.WriteAsync(_buffer.AsMemory(0, canCopy), cancellationToken);

            copied += canCopy;
        }
    }

    protected override void Dispose(bool disposing)
    {
        // dispose stream
        Stream stream = _stream;
        _stream = null!;
        try
        {
            if (disposing && !_leaveOpen && stream is not null)
                stream.Dispose();
        }
        finally
        {
            // dispose random
            _random = null!;
            // dispose buffer
            byte[]? buffer = _buffer;
            if (buffer is not null)
            {
                _buffer = null!;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // dispose stream
        Stream stream = _stream;
        _stream = null!;
        try
        {
            if (!_leaveOpen && stream is not null)
                await stream.DisposeAsync().ConfigureAwait(false);
        }
        catch { }
        // dispose rest
        Dispose(false);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_stream is null, this);
    }

    private void InitializeBuffer()
    {
        _buffer ??= ArrayPool<byte>.Shared.Rent(DEFAULT_BUFFER_SIZE);
    }

    private void InitializeChecksum()
    {
        if (_index == -1)
        {
            Checksum = _random.Next();
            _index = 0;
        }
    }

    private byte EncryptByte(byte b)
    {
        Checksum = b ^ BitOperations.RotateRight(Checksum, _index % 7 + 1);
        b ^= (byte)(_random.Next() >> (_index % 16));
        _index++;
        return b;
    }

    private void EncryptInternalBuffer(int byteCount)
    {
        for (int i = 0; i < byteCount; ++i)
            _buffer[i] = EncryptByte(_buffer[i]);
    }
}