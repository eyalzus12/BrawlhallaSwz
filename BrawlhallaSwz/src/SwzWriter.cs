using System;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzWriter : MarshalByRefObject, IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzWriter(Stream stream, uint key, uint seed = 0)
    {
        _stream = stream;
        _stream.Position = 0;
        _random = new(key ^ seed);
        uint checksum = SwzUtils.CalculateKeyChecksum(key, _random);
        _stream.WriteBigEndian(checksum);
        _stream.WriteBigEndian(seed);
    }

    public void WriteFile(string content)
    {
        // encode
        byte[] buffer = Encoding.UTF8.GetBytes(content);
        // compress
        byte[] compressedBuffer = SwzUtils.CompressBuffer(buffer);
        // calculate data
        uint compressedSize = (uint)compressedBuffer.Length ^ _random.Next();
        uint decompressedSize = (uint)buffer.Length ^ _random.Next();
        uint checksum = SwzUtils.CalculateBufferChecksum(compressedBuffer, _random.Next());
        // encrypt buffer
        SwzUtils.CipherBuffer(compressedBuffer, _random);
        // write data
        _stream.WriteBigEndian(compressedSize);
        _stream.WriteBigEndian(decompressedSize);
        _stream.WriteBigEndian(checksum);
        // write buffer
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
