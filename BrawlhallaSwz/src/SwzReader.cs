using System;
using System.IO;
using System.Text;

namespace BrawlhallaSwz;

public class SwzReader : MarshalByRefObject, IDisposable
{
    private readonly Stream _stream;
    private readonly SwzRandom _random;

    public SwzReader(Stream stream, uint key)
    {
        _stream = stream;
        _stream.Position = 0;
        uint checksum = _stream.ReadBigEndian<uint>();
        uint seed = _stream.ReadBigEndian<uint>();
        _random = new(seed ^ key);
        uint calculatedChecksum = SwzUtils.CalculateKeyChecksum(key, _random);
        if (calculatedChecksum != checksum) throw new SwzChecksumException($"Key checksum check failed. Expected {checksum} but got {calculatedChecksum}");
    }

    public string ReadFile()
    {
        // read data
        uint compressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint decompressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint checksum = _stream.ReadBigEndian<uint>();
        uint checksumInit = _random.Next();
        // read buffer
        byte[] compressedBuffer = _stream.ReadBuffer((int)compressedSize);
        // decrypt buffer
        SwzUtils.CipherBuffer(compressedBuffer, _random);
        // validate checksum
        uint calculatedChecksum = SwzUtils.CalculateBufferChecksum(compressedBuffer, checksumInit);
        if (calculatedChecksum != checksum) throw new SwzChecksumException($"File checksum check failed. Expected {checksum} but got {calculatedChecksum}");
        // decompress
        byte[] buffer = SwzUtils.DecompressBuffer(compressedBuffer);
        // validate size
        if (buffer.Length != decompressedSize) throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but file size is {buffer.Length}");
        // decode
        string content = Encoding.UTF8.GetString(buffer);
        // return
        return content;
    }

    public bool HasNext()
    {
        return _stream.CanRead && _stream.Position < _stream.Length;
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
