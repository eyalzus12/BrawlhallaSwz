using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzWriter(Stream stream, uint key, uint seed = 0)
    {
        _stream = stream; _stream.Position = 0;

        _random = new(key ^ seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(key, _random);

        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, checksum);
        _stream.Write(buffer);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, seed);
        _stream.Write(buffer);
    }

    public void WriteFile(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] compressedBytes = SwzUtils.CompressBuffer(bytes);
        uint compressedSize = (uint)compressedBytes.Length ^ _random.Next();
        uint decompressedSize = (uint)bytes.Length ^ _random.Next();
        uint checksum = SwzUtils.EncryptBuffer(compressedBytes, _random);

        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, compressedSize);
        _stream.Write(buffer);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, decompressedSize);
        _stream.Write(buffer);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, checksum);
        _stream.Write(buffer);

        _stream.Write(compressedBytes);
    }

    public void Flush()
    {
        _stream.Flush();
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

    ~SwzWriter()
    {
        Dispose(false);
    }
}