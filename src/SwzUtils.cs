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
        using MemoryStream bufferStream = new(buffer);
        using ZLibStream zlibStream = new(bufferStream, CompressionMode.Compress);
        using MemoryStream compressedStream = new(); zlibStream.CopyTo(compressedStream);
        byte[] compressedBuffer = compressedStream.ToArray();
        return compressedBuffer;
    }

    internal static byte[] DecompressBuffer(byte[] compressedBuffer)
    {
        using MemoryStream compressedStream = new(compressedBuffer);
        using ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress);
        using MemoryStream bufferStream = new(); zlibStream.CopyTo(bufferStream);
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
        uint checksum = 0x2DF4A1CD;
        uint hashRounds = key % 31 + 5;
        for (int i = 0; i < hashRounds; ++i)
        {
            checksum ^= rand.Next();
        }
        return checksum;
    }

    internal static void EncryptBuffer(byte[] buffer, SwzRandom rand, out uint checksum)
    {
        checksum = rand.Next();
        for (int i = 0; i < buffer.Length; ++i)
        {
            checksum = buffer[i] ^ BitOperations.RotateRight(checksum, i % 7 + 1);
            buffer[i] ^= (byte)(rand.Next() >> (i % 15));
        }
    }

    internal static void DecryptBuffer(byte[] buffer, SwzRandom rand, out uint checksum)
    {
        checksum = rand.Next();
        for (int i = 0; i < buffer.Length; ++i)
        {
            buffer[i] ^= (byte)(rand.Next() >> (i % 15));
            checksum = buffer[i] ^ BitOperations.RotateRight(checksum, i % 7 + 1);
        }
    }

    private static readonly Regex LevelDescRegex = LevelDescRegexGenerator();
    private static readonly Regex XmlRegex = XmlRegexGenerator();
    private static readonly Regex CsvRegex = CsvRegexGenerator();

    public static string GetFileName(string content)
    {
        //LevelDesc
        Match levelDescMatch = LevelDescRegex.Match(content);
        if (levelDescMatch.Success) return levelDescMatch.Captures[0].Value;
        //xml
        Match xmlMatch = XmlRegex.Match(content);
        if (xmlMatch.Success) return xmlMatch.Captures[0].Value;
        //csv
        Match csvMatch = CsvRegex.Match(content);
        if (csvMatch.Success) return csvMatch.Captures[0].Value;
        throw new SwzFileNameException("Could not find file name from file content as it does not match any known format");
    }

    [GeneratedRegex(@"^<LevelDesc AssetDir=""\w+"" LevelName=""(\w+)"">", RegexOptions.Compiled)]
    private static partial Regex LevelDescRegexGenerator();

    [GeneratedRegex(@"^<(\w+)>", RegexOptions.Compiled)]
    private static partial Regex XmlRegexGenerator();
    [GeneratedRegex(@"^(\w+)$", RegexOptions.Compiled)]
    private static partial Regex CsvRegexGenerator();
}
