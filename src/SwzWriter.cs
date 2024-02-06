using System;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzWriter(Stream stream, uint key, uint seed = 0)
    {
        _stream = stream;
        _random = new(key ^ seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(key, _random);
        _stream.WriteBigEndian(checksum);
        _stream.WriteBigEndian(seed);
    }

    public void WriteFile(string content)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        uint decompressedSize = (uint)buffer.Length ^ _random.Next();
        byte[] compressedBuffer = SwzUtils.CompressBuffer(buffer);
        uint compressedSize = (uint)compressedBuffer.Length ^ _random.Next();
        _stream.WriteBigEndian(compressedSize);
        _stream.WriteBigEndian(decompressedSize);
        SwzUtils.EncryptBuffer(compressedBuffer, _random, out uint checksum);
        _stream.WriteBigEndian(checksum);
        _stream.WriteBuffer(compressedBuffer);
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

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
    }

    ~SwzWriter()
    {
        Dispose(false);
    }
}
