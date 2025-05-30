using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz;

public class SwzWriter : IDisposable, IAsyncDisposable
{
    private Stream _stream;
    private readonly bool _leaveOpen;

    private byte[] _buffer = new byte[4];
    private MemoryStream? _intermediate = null;

    private readonly uint _key;
    private readonly uint _seed;
    private SwzRandom _random = null!;

    public SwzWriter(Stream stream, uint key, uint seed = 0, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new NotSupportedException("Given stream does not support writing");

        _stream = stream;
        _leaveOpen = leaveOpen;
        _key = key;
        _seed = seed;
    }

    private void EnsureWroteHeader()
    {
        if (_random is not null) return;
        _random = new(_key ^ _seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(_key, _random);
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, checksum);
        _stream.Write(_buffer);
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, _seed);
        _stream.Write(_buffer);
    }

    private async ValueTask EnsureWroteHeaderAsync(CancellationToken cancellationToken = default)
    {
        if (_random is not null) return;
        _random = new(_key ^ _seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(_key, _random);
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, checksum);
        await _stream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, _seed);
        await _stream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
    }

    public void WriteFile(Stream stream)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new NotSupportedException("Given stream does not support reading");

        WriteFileCore(stream);
    }

    private void WriteFileCore(Stream stream)
    {
        EnsureWroteHeader();

        long decompressedSize_ = stream.Length;
        if (decompressedSize_ > uint.MaxValue) throw new OverflowException("Size of given stream exceeds uint32 max");

        // the sizes are written before the stream, so these random calls have to be stored
        uint compressedSizeSalt = _random.Next();
        uint decompressedSizeSalt = _random.Next();

        // we have to write the checksum before writing the content, so we have to consume the stream.
        _intermediate ??= new();
        _intermediate.SetLength(0);

        uint checksum;
        using (SwzEncryptStream encryptor = new(_intermediate, _random, true))
        {
            using (ZLibStream zLibStream = new(encryptor, CompressionLevel.SmallestSize, true))
                stream.CopyTo(zLibStream);
            checksum = encryptor.Checksum;
        }

        long compressedSize_ = _intermediate.Length;
        if (compressedSize_ > uint.MaxValue) throw new OverflowException("Size of compressed file exceeds uint32 max");

        // write file data

        // write compressed size
        uint compressedSize = (uint)compressedSize_ ^ compressedSizeSalt;
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, compressedSize);
        _stream.Write(_buffer);
        // write decompressed size
        uint decompressedSize = (uint)decompressedSize_ ^ decompressedSizeSalt;
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, decompressedSize);
        _stream.Write(_buffer);
        // write checksum
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, checksum);
        _stream.Write(_buffer);
        // write file
        _intermediate.WriteTo(_stream);
    }

    public async ValueTask WriteFileAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new NotSupportedException("Given stream does not support reading");

        await WriteFileCoreAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask WriteFileCoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await EnsureWroteHeaderAsync(cancellationToken).ConfigureAwait(false);

        long decompressedSize_ = stream.Length;
        if (decompressedSize_ > uint.MaxValue) throw new OverflowException("Size of given stream exceeds uint32 max");

        // the sizes are written before the stream, so these random calls have to be stored
        uint compressedSizeSalt = _random.Next();
        uint decompressedSizeSalt = _random.Next();

        // we have to write the checksum before writing the content, so we have to consume the stream.
        _intermediate ??= new();
        _intermediate.SetLength(0);

        uint checksum;
        using (SwzEncryptStream encryptor = new(_intermediate, _random, true))
        {
            using (ZLibStream zLibStream = new(encryptor, CompressionLevel.SmallestSize, true))
                await stream.CopyToAsync(zLibStream, cancellationToken).ConfigureAwait(false);
            checksum = encryptor.Checksum;
        }

        long compressedSize_ = _intermediate.Length;
        if (compressedSize_ > uint.MaxValue) throw new OverflowException("Size of compressed file exceeds uint32 max");

        // write file data

        // write compressed size
        uint compressedSize = (uint)compressedSize_ ^ compressedSizeSalt;
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, compressedSize);
        await _stream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
        // write decompressed size
        uint decompressedSize = (uint)decompressedSize_ ^ decompressedSizeSalt;
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, decompressedSize);
        await _stream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
        // write checksum
        BinaryPrimitives.WriteUInt32BigEndian(_buffer, checksum);
        await _stream.WriteAsync(_buffer, cancellationToken).ConfigureAwait(false);
        // write file
        _intermediate.Position = 0;
        await _intermediate.CopyToAsync(_stream, cancellationToken).ConfigureAwait(false);
    }

    public void WriteFile(string content)
    {
        EnsureNotDisposed();

        byte[] bytes = Encoding.UTF8.GetBytes(content);
        using MemoryStream ms = new(bytes);
        WriteFileCore(ms);
    }

    public ValueTask WriteFileAsync(string content, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled(cancellationToken);
        EnsureNotDisposed();
        return Core();

        async ValueTask Core()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using MemoryStream ms = new(bytes);
            await WriteFileCoreAsync(ms, cancellationToken);
        }
    }

    public void Flush()
    {
        EnsureNotDisposed();
        _stream.Flush();
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);
        EnsureNotDisposed();
        return _stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
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
            if (_intermediate is not null)
            {
                _intermediate.Dispose();
                _intermediate = null!;
            }
            _random = null!;
            _buffer = null!;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        Stream stream = _stream;
        _stream = null!;
        if (!_leaveOpen && stream is not null)
            await stream.DisposeAsync().ConfigureAwait(false);
    }

    ~SwzWriter()
    {
        Dispose(false);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_stream is null, this);
    }
}