using System;
using System.IO;
using BrawlhallaSwz;

public class Sample
{
    public static void DecryptFile(string path, uint key)
    {
        string dumpPath = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + " - dump");
        Directory.CreateDirectory(dumpPath);
        using FileStream file = new(path, FileMode.Open, FileAccess.Read);
        using SwzReader swz = new(file, key);
        while (swz.HasNext())
        {
            string fileContent = swz.ReadFile();
            string fileName = SwzUtils.GetFileName(fileContent);
            Console.WriteLine($"Got file {fileName}");
            string filePath = Path.Combine(dumpPath, fileName);
            File.WriteAllText(filePath, fileContent);
        }
    }

    public static void EncryptDirectory(string path, uint key, uint seed)
    {
        string outputFilePath = Path.ChangeExtension(path, "swz");
        using FileStream swzFile = new(outputFilePath, FileMode.Create, FileAccess.Write);
        using SwzWriter swz = new(swzFile, key, seed);
        foreach (string filePath in Directory.EnumerateFiles(path))
        {
            Console.WriteLine($"Encrypting {filePath}");
            string fileContent = File.ReadAllText(filePath);
            swz.WriteFile(fileContent);
        }
    }
}