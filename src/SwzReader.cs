using System;
using System.Diagnostics.CodeAnalysis;
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
        if (calculatedChecksum != checksum)
        {
            throw new SwzChecksumException("Key checksum check failed");
        }
    }

    public string ReadFile()
    {
        uint compressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint decompressedSize = _stream.ReadBigEndian<uint>() ^ _random.Next();
        uint checksum = _stream.ReadBigEndian<uint>();
        byte[] buffer = _stream.ReadBuffer((int)compressedSize);
        SwzUtils.DecryptBuffer(buffer, _random, out uint calculatedChecksum);
        if (calculatedChecksum != checksum)
        {
            throw new SwzChecksumException("File checksum check failed");
        }
        byte[] contentBytes = SwzUtils.DecompressBuffer(buffer);
        if (contentBytes.Length != decompressedSize)
        {
            throw new SwzFileSizeException($"Expected file size to be {decompressedSize}, but file size is {contentBytes.Length}");
        }
        string content = Encoding.UTF8.GetString(contentBytes);
        return content;
    }

    public bool TryReadFile([NotNullWhen(true)] out string? content)
    {
        if (_stream.Position >= _stream.Length)
        {
            content = null;
            return false;
        }
        else
        {
            content = ReadFile();
            return true;
        }
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

    ~SwzReader()
    {
        Dispose(false);
    }
}
