using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz;

[Flags]
public enum SwzReaderOptions
{
    None = 0,
    // Don't check the key checksum is correct.
    IgnoreKeyChecksum = 0b0001,
    // Don't check the file checksum is correct.
    IgnoreFileChecksum = 0b0010,
    // Don't check the file length is correct.
    NoCheckFileLength = 0b0100,
    // Avoid writing to the stream if validation fails.
    // Requires using an intermediate buffer.
    AvoidWriteIfValidationFails = 0b1000,
}

public class SwzReader : IDisposable, IAsyncDisposable
{
    private Stream _stream;
    private readonly bool _leaveOpen;
    public SwzReaderOptions Options { get; set; }
    private bool IgnoreKeyChecksum => (Options & SwzReaderOptions.IgnoreKeyChecksum) != 0;
    private bool IgnoreFileChecksum => (Options & SwzReaderOptions.IgnoreFileChecksum) != 0;
    private bool NoCheckFileLength => (Options & SwzReaderOptions.NoCheckFileLength) != 0;
    private bool AvoidWriteIfValidationFails => (Options & SwzReaderOptions.AvoidWriteIfValidationFails) != 0;

    private byte[] _buffer = new byte[4];

    private readonly uint _key;
    private SwzRandom _random = null!;

    public SwzReader(Stream stream, uint key, SwzReaderOptions options = SwzReaderOptions.None, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new NotSupportedException("Given stream does not support reading");

        _stream = stream;
        _leaveOpen = leaveOpen;
        _key = key;
        Options = options;
    }

    private void InitializeRandom(uint seed, uint checksum)
    {
        _random = new(seed ^ _key);
        // has to be done even if ignored because it modifies the prng state
        uint calculatedChecksum = SwzUtils.CalculateKeyChecksum(_key, _random);
        if (!IgnoreKeyChecksum && calculatedChecksum != checksum)
            throw new SwzKeyChecksumException($"Key checksum check failed. Expected {checksum} but got {calculatedChecksum}.");
    }

    private bool EnsureReadHeader()
    {
        if (_random is not null) return false;

        _stream.ReadExactly(_buffer);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
        _stream.ReadExactly(_buffer);
        uint seed = BinaryPrimitives.ReadUInt32BigEndian(_buffer);

        InitializeRandom(seed, checksum);
        return true;
    }

    private async ValueTask<bool> EnsureReadHeaderAsync(CancellationToken cancellationToken = default)
    {
        if (_random is not null) return false;

        await _stream.ReadExactlyAsync(_buffer, cancellationToken).ConfigureAwait(false);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(_buffer);
        await _stream.ReadExactlyAsync(_buffer, cancellationToken).ConfigureAwait(false);
        uint seed = BinaryPrimitives.ReadUInt32BigEndian(_buffer);

        InitializeRandom(seed, checksum);
        return true;
    }

    public void ReadFile(Stream destStream)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(destStream);
        if (!destStream.CanWrite) throw new NotSupportedException("Given stream does not support writing");

        ReadFileCore(destStream);
    }

    private void ReadFileCore(Stream destStream)
    {
        EnsureReadHeader();

        _stream.ReadExactly(_buffer);
        uint compressedSize = BinaryPrimitives.ReadUInt32BigEndian(_buffer) ^ _random.Next();

        _stream.ReadExactly(_buffer);
        uint decompressedSize = BinaryPrimitives.ReadUInt32BigEndian(_buffer) ^ _random.Next();

        _stream.ReadExactly(_buffer);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(_buffer);

        using MemoryStream? intermediate = AvoidWriteIfValidationFails ? new() : null;

        long amountCopied;
        uint computedChecksum;
        using Internal.SubStream sub = new(_stream, compressedSize);
        using (SwzDecryptStream decryptor = new(sub, _random, true))
        using (ZLibStream zLibStream = new(decryptor, CompressionMode.Decompress, true))
        {
            amountCopied = SwzUtils.CopyStream(zLibStream, intermediate ?? destStream, decompressedSize);
            computedChecksum = decryptor.Checksum;
        }

        // validate. TODO: extract this logic to share it with the async version
        if (!NoCheckFileLength)
        {
            // did not copy everything. decompressedSize is too big!
            if (amountCopied != decompressedSize)
                throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but it was only {amountCopied}.");
            // did not read everything. compressedSize is too big!
            if (sub.Position != sub.Length)
                throw new SwzFileSizeException($"Expected compressed file size to be {compressedSize}, but it was actually {sub.Position}");
        }
        // compare checksum
        if (!IgnoreFileChecksum && computedChecksum != checksum)
            throw new SwzFileChecksumException($"File checksum check failed. Expected {checksum} but got {computedChecksum}.");

        // copy if using intermediate stream
        if (intermediate is not null)
        {
            intermediate.Position = 0;
            intermediate.CopyTo(destStream);
        }
    }

    public ValueTask ReadFileAsync(Stream destStream, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        ArgumentNullException.ThrowIfNull(destStream);
        if (!destStream.CanWrite) throw new NotSupportedException("Given stream does not support writing");

        return ReadFileCoreAsync(destStream, cancellationToken);
    }

    private async ValueTask ReadFileCoreAsync(Stream destStream, CancellationToken cancellationToken = default)
    {
        await EnsureReadHeaderAsync(cancellationToken).ConfigureAwait(false);

        await _stream.ReadExactlyAsync(_buffer, cancellationToken).ConfigureAwait(false);
        uint compressedSize = BinaryPrimitives.ReadUInt32BigEndian(_buffer) ^ _random.Next();

        await _stream.ReadExactlyAsync(_buffer, cancellationToken).ConfigureAwait(false);
        uint decompressedSize = BinaryPrimitives.ReadUInt32BigEndian(_buffer) ^ _random.Next();

        await _stream.ReadExactlyAsync(_buffer, cancellationToken).ConfigureAwait(false);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(_buffer);

        using MemoryStream? intermediate = AvoidWriteIfValidationFails ? new() : null;

        long amountCopied;
        uint computedChecksum;
        using Internal.SubStream sub = new(_stream, compressedSize);
        using (SwzDecryptStream decryptor = new(sub, _random, true))
        using (ZLibStream zLibStream = new(decryptor, CompressionMode.Decompress, true))
        {
            amountCopied = await SwzUtils.CopyStreamAsync(zLibStream, intermediate ?? destStream, decompressedSize, cancellationToken).ConfigureAwait(false);
            computedChecksum = decryptor.Checksum;
        }

        // validate. TODO: extract this logic to share it with the sync version
        if (!NoCheckFileLength)
        {
            // did not copy everything. decompressedSize is too big!
            if (amountCopied != decompressedSize)
                throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but it was only {amountCopied}.");
            // did not read everything. compressedSize is too big!
            if (sub.Position != sub.Length)
                throw new SwzFileSizeException($"Expected compressed file size to be {compressedSize}, but it was actually {sub.Position}");
        }
        // compare checksum
        if (!IgnoreFileChecksum && computedChecksum != checksum)
            throw new SwzFileChecksumException($"File checksum check failed. Expected {checksum} but got {computedChecksum}.");

        // copy if using intermediate stream
        if (intermediate is not null)
        {
            intermediate.Position = 0;
            await intermediate.CopyToAsync(destStream, cancellationToken).ConfigureAwait(false);
        }
    }

    public string ReadFile()
    {
        EnsureNotDisposed();

        using MemoryStream ms = new();
        ReadFileCore(ms);
        ms.Position = 0;
        using StreamReader sr = new(ms, new UTF8Encoding(false));
        return sr.ReadToEnd();
    }

    public async Task<string> ReadFileAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        using MemoryStream ms = new();
        await ReadFileCoreAsync(ms, cancellationToken).ConfigureAwait(false);
        ms.Position = 0;
        using StreamReader sr = new(ms, new UTF8Encoding(false));
        return await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<string> ReadFiles()
    {
        EnsureNotDisposed();

        using MemoryStream ms = new();
        using StreamReader sr = new(ms, new UTF8Encoding(false), leaveOpen: true);
        while (HasNext())
        {
            ms.SetLength(0); // also sets Position to 0
            ReadFileCore(ms);
            ms.Position = 0;
            yield return sr.ReadToEnd();
        }
    }

    public async IAsyncEnumerable<string> ReadFilesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();

        using MemoryStream ms = new();
        using StreamReader sr = new(ms, new UTF8Encoding(false), leaveOpen: true);
        while (HasNext())
        {
            ms.SetLength(0); // also sets Position to 0
            await ReadFileCoreAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;
            yield return await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public bool HasNext()
    {
        if (_stream is null) return false;
        return _stream.CanRead && _stream.Position < _stream.Length;
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

    ~SwzReader()
    {
        Dispose(false);
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_stream is null, this);
    }
}