using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.RegularExpressions;

namespace BrawlhallaSwz;

public static partial class SwzUtils
{
    internal static byte[] CompressBuffer(byte[] buffer)
    {
        using MemoryStream compressedStream = new();
        using (ZLibStream zlibStream = new(compressedStream, CompressionLevel.SmallestSize))
        {
            using MemoryStream bufferStream = new(buffer);
            bufferStream.CopyTo(zlibStream);
        }

        byte[] compressedBuffer = compressedStream.ToArray();
        return compressedBuffer;
    }

    internal static byte[] DecompressBuffer(byte[] compressedBuffer)
    {
        using MemoryStream bufferStream = new();

        using (MemoryStream compressedStream = new(compressedBuffer))
        using (ZLibStream zlibStream = new(compressedStream, CompressionMode.Decompress))
        {
            zlibStream.CopyTo(bufferStream);
        }

        byte[] buffer = bufferStream.ToArray();
        return buffer;
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

    internal static uint EncryptBuffer(Span<byte> buffer, SwzRandom rand)
    {
        uint checksum = rand.Next();
        for (int i = 0; i < buffer.Length; ++i)
        {
            checksum = buffer[i] ^ BitOperations.RotateRight(checksum, i % 7 + 1);
            buffer[i] ^= (byte)(rand.Next() >> (i % 16));
        }
        return checksum;
    }

    internal static uint DecryptBuffer(Span<byte> buffer, SwzRandom rand)
    {
        uint checksum = rand.Next();
        for (int i = 0; i < buffer.Length; ++i)
        {
            buffer[i] ^= (byte)(rand.Next() >> (i % 16));
            checksum = buffer[i] ^ BitOperations.RotateRight(checksum, i % 7 + 1);
        }
        return checksum;
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

    [GeneratedRegex(@"^<LevelDesc AssetDir=""\w+""\s+LevelName=""(\w+)"".*?>", RegexOptions.Compiled)]
    private static partial Regex LevelDescRegexGenerator();
    [GeneratedRegex(@"^<\s*(\w+)\s*>", RegexOptions.Compiled)]
    private static partial Regex XmlRegexGenerator();
    [GeneratedRegex(@"^(\w+)\n", RegexOptions.Compiled)]
    private static partial Regex CsvRegexGenerator();
}