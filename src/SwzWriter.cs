using System;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzWriter(Stream stream, uint key)
    {
        _stream = stream;
        uint seed = (uint)Random.Shared.Next();
        _random = new(key ^ seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(key, _random);
        _stream.WriteBigEndian(checksum);
        _stream.WriteBigEndian(seed);
    }

    public void WriteFile(string content)
    {
        byte[] contentBuffer = Encoding.UTF8.GetBytes(content);
        uint decompressedSize = (uint)contentBuffer.Length ^ _random.Next();
        byte[] buffer = SwzUtils.CompressBuffer(contentBuffer);
        uint compressedSize = (uint)buffer.Length ^ _random.Next();
        _stream.WriteBigEndian(compressedSize);
        _stream.WriteBigEndian(decompressedSize);
        SwzUtils.EncryptBuffer(buffer, _random, out uint checksum);
        _stream.WriteBigEndian(checksum);
        _stream.WriteBuffer(buffer);
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
