using System;
using System.Buffers;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BrawlhallaSwz;

public static partial class SwzUtils
{
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

    // why is this not a standard function?
    internal static long CopyStream(Stream source, Stream destination, long bytes)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long bytesRead = 0;
            while (bytesRead < bytes)
            {
                // read
                int toRead = Math.Min(buffer.Length, (int)bytes);
                int read = source.Read(buffer.AsSpan(0, toRead));
                if (read == 0) break;
                //write
                destination.Write(buffer.AsSpan(0, read));

                bytesRead += read;
            }
            return bytesRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static ValueTask<long> CopyStreamAsync(Stream source, Stream destination, long bytes, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return ValueTask.FromCanceled<long>(cancellationToken);

        return Core(source, destination, bytes, cancellationToken);

        static async ValueTask<long> Core(Stream source, Stream destination, long bytes, CancellationToken cancellationToken = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                long bytesRead = 0;
                while (bytesRead < bytes)
                {
                    // read
                    int toRead = Math.Min(buffer.Length, (int)bytes);
                    int read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    //write
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

                    bytesRead += read;
                }
                return bytesRead;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public static string GetFileName(string content)
    {
        // LevelDesc
        Match levelDescMatch = LevelDescRegex().Match(content);
        if (levelDescMatch.Success) return "LevelDesc_" + levelDescMatch.Groups[1].Value + ".xml";

        // CutsceneType
        Match cutsceneTypeMatch = CutsceneTypeRegex().Match(content);
        if (cutsceneTypeMatch.Success) return "CutsceneType_" + cutsceneTypeMatch.Groups[1].Value + ".xml";

        // xml
        Match xmlMatch = XmlRegex().Match(content);
        if (xmlMatch.Success) return xmlMatch.Groups[1].Value + ".xml";

        // csv
        Match csvMatch = CsvRegex().Match(content);
        if (csvMatch.Success) return csvMatch.Groups[1].Value + ".csv";

        throw new SwzFileNameException("Could not find file name from file content as it does not match any known format");
    }

    [GeneratedRegex(@"^<LevelDesc AssetDir="".+?""\s+LevelName=""(.+?)"".*?>")]
    private static partial Regex LevelDescRegex();
    [GeneratedRegex(@"^<CutsceneType CutsceneName=""(.+?)""\s+CutsceneID=""[0-9]+"".*?>")]
    private static partial Regex CutsceneTypeRegex();
    [GeneratedRegex(@"^<\s*(\w+)\s*>")]
    private static partial Regex XmlRegex();
    [GeneratedRegex(@"^(\w+)\n")]
    private static partial Regex CsvRegex();
}