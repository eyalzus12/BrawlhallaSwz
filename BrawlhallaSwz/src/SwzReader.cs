using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzReader : IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzReader(Stream stream, uint key, bool ignoreChecksum = false)
    {
        _stream = stream; _stream.Position = 0;

        Span<byte> buffer = stackalloc byte[4];
        _stream.ReadExactly(buffer);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        _stream.ReadExactly(buffer);
        uint seed = BinaryPrimitives.ReadUInt32BigEndian(buffer);

        _random = new(seed ^ key);
        uint calculatedChecksum = SwzUtils.CalculateKeyChecksum(key, _random);
        if (!ignoreChecksum && calculatedChecksum != checksum)
            throw new SwzKeyChecksumException($"Key checksum check failed. Expected {checksum} but got {calculatedChecksum}.");
    }

    public string ReadFile(bool ignoreChecksum = false, bool ignoreLengthCheck = false)
    {
        Span<byte> buffer = stackalloc byte[4];
        _stream.ReadExactly(buffer);
        uint compressedSize = BinaryPrimitives.ReadUInt32BigEndian(buffer) ^ _random.Next();
        _stream.ReadExactly(buffer);
        uint decompressedSize = BinaryPrimitives.ReadUInt32BigEndian(buffer) ^ _random.Next();
        _stream.ReadExactly(buffer);
        uint checksum = BinaryPrimitives.ReadUInt32BigEndian(buffer);

        byte[] compressedBytes = new byte[(int)compressedSize];
        _stream.ReadExactly(compressedBytes);

        uint calculatedChecksum = SwzUtils.DecryptBuffer(compressedBytes, _random);
        if (!ignoreChecksum && calculatedChecksum != checksum)
            throw new SwzFileChecksumException($"File checksum check failed. Expected {checksum} but got {calculatedChecksum}.");

        byte[] bytes = SwzUtils.DecompressBuffer(compressedBytes);
        if (!ignoreLengthCheck && bytes.Length != decompressedSize)
            throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but file size is {bytes.Length}.");

        string content = Encoding.UTF8.GetString(bytes);
        return content;
    }

    public bool HasNext()
    {
        return _stream.CanRead && _stream.Position < _stream.Length;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
        }
        disposed = true;
    }

    ~SwzReader()
    {
        Dispose(false);
    }
}