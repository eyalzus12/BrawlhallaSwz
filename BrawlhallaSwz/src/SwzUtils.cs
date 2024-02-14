using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace BrawlhallaSwz;

public static partial class SwzUtils
{
    internal static byte[] CompressBuffer(byte[] buffer)
    {
        // create compressor
        using MemoryStream compressedStream = new();
        using (ZLibStream zlibStream = new(compressedStream, CompressionLevel.SmallestSize))
        {
            // write buffer into compressor
            using MemoryStream bufferStream = new(buffer);
            bufferStream.CopyTo(zlibStream);
        }
        // get compressed buffer
        byte[] compressedBuffer = compressedStream.ToArray();
        return compressedBuffer;
    }

    internal static byte[] DecompressBuffer(byte[] compressedBuffer)
    {
        // create decompressor
        using MemoryStream compressedStream = new(compressedBuffer);
        using ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress);
        // extract decompressed buffer
        using MemoryStream bufferStream = new();
        zlibStream.CopyTo(bufferStream);
        // get decompressed buffer
        byte[] buffer = bufferStream.ToArray();
        return buffer;
    }

    internal static T ReadBigEndian<T>(this Stream stream) where T : unmanaged, IBinaryInteger<T>
    {
        byte[] bytes = new byte[Unsafe.SizeOf<T>()];
        stream.ReadExactly(bytes, 0, bytes.Length);
        return T.ReadBigEndian(bytes, true);
    }

    internal static void WriteBigEndian<T>(this Stream stream, T number) where T : unmanaged, IBinaryInteger<T>
    {
        byte[] bytes = new byte[Unsafe.SizeOf<T>()];
        number.WriteBigEndian(bytes);
        stream.Write(bytes, 0, bytes.Length);
    }

    internal static byte[] ReadBuffer(this Stream stream, int amount)
    {
        byte[] buffer = new byte[amount];
        stream.ReadExactly(buffer, 0, amount);
        return buffer;
    }

    internal static void WriteBuffer(this Stream stream, byte[] buffer)
    {
        stream.Write(buffer, 0, buffer.Length);
    }

    internal static uint CalculateKeyChecksum(uint key, SwzRandom rand)
    {
        uint checksum = 0x2DF4A1CDu;
        uint rounds = key % 31 + 5;
        for (uint i = 0; i < rounds; ++i)
        {
            checksum ^= rand.Next();
        }
        return checksum;
    }

    internal static uint CalculateBufferChecksum(byte[] buffer, uint checksumInit)
    {
        uint checksum = checksumInit;
        for (int i = 0; i < buffer.Length; ++i)
        {
            checksum = buffer[i] ^ BitOperations.RotateRight(checksum, i % 7 + 1);
        }
        return checksum;
    }

    internal static void CipherBuffer(byte[] buffer, SwzRandom rand)
    {
        for (int i = 0; i < buffer.Length; ++i)
        {
            buffer[i] ^= (byte)(rand.Next() >> (i % 16));
        }
    }

    private static readonly Regex LevelDescRegex = LevelDescRegexGenerator();
    private static readonly Regex XmlRegex = XmlRegexGenerator();
    private static readonly Regex CsvRegex = CsvRegexGenerator();

    public static string GetFileName(string content)
    {
        // LevelDesc
        Match levelDescMatch = LevelDescRegex.Match(content);
        if (levelDescMatch.Success) return levelDescMatch.Groups[1].Value + ".xml";
        // xml
        Match xmlMatch = XmlRegex.Match(content);
        if (xmlMatch.Success) return xmlMatch.Groups[1].Value + ".xml";
        // csv
        Match csvMatch = CsvRegex.Match(content);
        if (csvMatch.Success) return csvMatch.Groups[1].Value + ".csv";
        throw new SwzFileNameException("Could not find file name from file content as it does not match any known format");
    }

    [GeneratedRegex(@"^<LevelDesc AssetDir=""\w+"" LevelName=""(\w+)"">", RegexOptions.Compiled)]
    private static partial Regex LevelDescRegexGenerator();

    [GeneratedRegex(@"^<(\w+)>", RegexOptions.Compiled)]
    private static partial Regex XmlRegexGenerator();
    [GeneratedRegex(@"^(\w+)\n", RegexOptions.Compiled)]
    private static partial Regex CsvRegexGenerator();
}
