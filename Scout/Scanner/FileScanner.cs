using System.Security.Cryptography;
using Scout.Crypto;
using Scout.Data;
using Scout.Models;
using Scout.Xml;

namespace Scout.Scanner;

public class FileScanner
{
    private readonly string _sourceRoot;
    private readonly string _collectRoot;
    private readonly string _machineName;
    private readonly AesDecryptor? _decryptor;
    private readonly FileRecordRepository _repo;

    public FileScanner(
        string sourceRoot,
        string collectRoot,
        string machineName,
        AesDecryptor? decryptor,
        FileRecordRepository repo)
    {
        _sourceRoot  = sourceRoot;
        _collectRoot = collectRoot;
        _machineName = machineName;
        _decryptor   = decryptor;
        _repo        = repo;
    }

    public ScanSummary Run()
    {
        var summary = new ScanSummary();
        var files   = Directory.EnumerateFiles(_sourceRoot, "*.xml", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            summary.Total++;
            try
            {
                ProcessFile(filePath, summary);
            }
            catch (Exception ex)
            {
                summary.Errors++;
                Console.Error.WriteLine($"[ERROR] {filePath}: {ex.Message}");
            }
        }
        return summary;
    }

    private void ProcessFile(string filePath, ScanSummary summary)
    {
        var rawBytes = File.ReadAllBytes(filePath);
        var format   = XmlIdentifier.Detect(rawBytes);

        if (format == XmlFormat.Unknown)
        {
            summary.Skipped++;
            Console.WriteLine($"[SKIP]  {filePath} — not a recognised MSI XML");
            return;
        }

        byte[] plaintextBytes;
        bool isEncrypted = format == XmlFormat.Encrypted;

        if (isEncrypted)
        {
            if (_decryptor is null)
                throw new InvalidOperationException("secrets.key required to decrypt this file.");

            plaintextBytes = _decryptor.DecryptXml(rawBytes);
        }
        else
        {
            plaintextBytes = rawBytes;
        }

        var hash = ComputeMd5(plaintextBytes);

        if (_repo.ExistsByHash(hash, _machineName))
        {
            summary.Duplicates++;
            Console.WriteLine($"[DUP]   {filePath}");
            return;
        }

        var fields = XmlParser.Parse(plaintextBytes);
        var info   = new FileInfo(filePath);

        var record = new FileRecord
        {
            FileHash         = hash,
            OriginalFileName = info.Name,
            OriginalPath     = filePath,
            MachineName      = _machineName,
            FileSize         = info.Length,
            IsEncrypted      = isEncrypted,
            LastWriteTime    = info.LastWriteTimeUtc,
            CollectedAt      = DateTime.UtcNow,
            SurveyJobNo      = fields?.SurveyJobNo,
            OwnerName        = fields?.OwnerName,
            QueueDate        = fields?.QueueDate,
            ProvinceName     = fields?.ProvinceName,
            AmphurSeq        = fields?.AmphurSeq,
            TambolSeq        = fields?.TambolSeq,
            LandNo           = fields?.LandNo,
            SurveyNo         = fields?.SurveyNo,
            SurveyorName     = fields?.SurveyorName,
        };

        _repo.Upsert(record);
        CopyFile(filePath, rawBytes);

        summary.Collected++;
        Console.WriteLine($"[OK]    {filePath} [{(isEncrypted ? "ENC" : "PLAIN")}] {fields?.SurveyJobNo ?? "-"}");
    }

    private void CopyFile(string sourcePath, byte[] rawBytes)
    {
        // Preserve directory structure: Collected_XMLs\[MachineName]\[relative path from source root]
        var relative = Path.GetRelativePath(_sourceRoot, sourcePath);
        var dest     = Path.Combine(_collectRoot, _machineName, relative);

        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        // Always copy the original (possibly encrypted) file — never the decrypted plaintext
        File.WriteAllBytes(dest, rawBytes);
    }

    private static string ComputeMd5(byte[] data)
    {
        var hash = MD5.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public class ScanSummary
{
    public int Total      { get; set; }
    public int Collected  { get; set; }
    public int Duplicates { get; set; }
    public int Skipped    { get; set; }
    public int Errors     { get; set; }
}
