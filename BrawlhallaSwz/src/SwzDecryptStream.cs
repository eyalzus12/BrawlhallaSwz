using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz;

public sealed class SwzDecryptStream : Stream
{
    private const int DEFAULT_BUFFER_SIZE = 8192;

    private Stream _stream;
    private readonly bool _leaveOpen;
    private SwzRandom _random;

    public uint Checksum { get; private set; } = 0;
    private int _index = -1;
    private byte[] _buffer = null!;

    public SwzDecryptStream(Stream stream, SwzRandom random, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(random);
        if (!stream.CanRead) throw new NotSupportedException("Given stream is not readable");

        _stream = stream;
        _leaveOpen = leaveOpen;
        _random = random;
    }

    public override bool CanRead => _stream?.CanRead ?? false;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _stream?.Length ?? 0;
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
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

        InitializeBuffer();

        // cut down our buffer if it is too long
        Span<byte> tempBuffer = _buffer.AsSpan();
        if (tempBuffer.Length > buffer.Length) tempBuffer = tempBuffer[..buffer.Length];

        int read = _stream.Read(tempBuffer);
        if (read == 0) return 0;

        InitializeChecksum();
        DecryptInternalBuffer(read);

        // copy over whatever was read
        _buffer.AsSpan(0, read).CopyTo(buffer);

        return read;
    }

    public override int ReadByte()
    {
        EnsureNotDisposed();

        int b = _stream.ReadByte();
        if (b == -1) return b;

        InitializeChecksum();
        return DecryptByte((byte)b);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();

        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<int>(cancellationToken);

        return Core(buffer, cancellationToken);

        async ValueTask<int> Core(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            InitializeBuffer();

            // cut down our buffer if it is too long
            Memory<byte> tempBuffer = _buffer;
            if (tempBuffer.Length > buffer.Length) tempBuffer = tempBuffer[..buffer.Length];

            int read = await _stream.ReadAsync(tempBuffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) return 0;

            InitializeChecksum();
            DecryptInternalBuffer(read);

            // copy over whatever was read
            _buffer.AsMemory(0, read).CopyTo(buffer);

            return read;
        }
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
        throw new NotSupportedException();
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
            _random = null!;

            byte[]? buffer = _buffer;
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
                _buffer = null!;
            }
        }
    }

    public override async ValueTask DisposeAsync()
    {
        Stream stream = _stream;
        _stream = null!;
        if (!_leaveOpen && stream is not null)
            await stream.DisposeAsync().ConfigureAwait(false);

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

    private byte DecryptByte(byte b)
    {
        b ^= (byte)(_random.Next() >> (_index % 16));
        Checksum = b ^ BitOperations.RotateRight(Checksum, _index % 7 + 1);
        ++_index;
        return b;
    }

    private void DecryptInternalBuffer(int byteCount)
    {
        for (int i = 0; i < byteCount; ++i)
            _buffer[i] = DecryptByte(_buffer[i]);
    }
}