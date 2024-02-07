using System;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzReader : IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzReader(Stream stream, uint key)
    {
        _stream = stream;
        uint checksum = _stream.ReadBigEndian<uint>();
        uint seed = _stream.ReadBigEndian<uint>();
        _random = new(seed ^ key);
        uint calculatedChecksum = SwzUtils.CalculateKeyChecksum(key, _random);
        if (calculatedChecksum != checksum) throw new SwzChecksumException($"Key checksum check failed. Expected {checksum} but got {calculatedChecksum}");
    }

    public string ReadFile()
    {
        uint compressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint decompressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint checksum = _stream.ReadBigEndian<uint>();
        byte[] compressedBuffer = _stream.ReadBuffer((int)compressedSize);
        SwzUtils.DecryptBuffer(compressedBuffer, _random, out uint calculatedChecksum);
        if (calculatedChecksum != checksum) throw new SwzChecksumException($"File checksum check failed. Expected {checksum} but got {calculatedChecksum}");
        byte[] buffer = SwzUtils.DecompressBuffer(compressedBuffer);
        if (buffer.Length != decompressedSize) throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but file size is {buffer.Length}");
        string content = Encoding.UTF8.GetString(buffer);
        return content;
    }

    public bool HasNext()
    {
        return _stream.Position < _stream.Length;
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

    ~SwzReader()
    {
        Dispose(false);
    }
}
