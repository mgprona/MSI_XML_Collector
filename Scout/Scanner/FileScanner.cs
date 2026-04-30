using System.Security.Cryptography;
using Scout.Crypto;
using Scout.Data;
using Scout.Models;
using Scout.Xml;

namespace Scout.Scanner;

public class FileScanner
{
    private readonly IReadOnlyList<ScanTarget> _targets;
    private readonly string _collectRoot;
    private readonly string _machineName;
    private readonly AesDecryptor? _decryptor;
    private readonly FileRecordRepository _repo;

    public FileScanner(
        IReadOnlyList<ScanTarget> targets,
        string collectRoot,
        string machineName,
        AesDecryptor? decryptor,
        FileRecordRepository repo)
    {
        _targets     = targets;
        _collectRoot = collectRoot;
        _machineName = machineName;
        _decryptor   = decryptor;
        _repo        = repo;
    }

    public ScanSummary Run()
    {
        var summary = new ScanSummary();

        foreach (var target in _targets)
        {
            if (!Directory.Exists(target.ScanPath))
            {
                Console.WriteLine($"[SKIP]  {target.ScanPath} — ไม่พบโฟลเดอร์");
                continue;
            }

            Console.WriteLine($"[SCAN]  {target.ScanPath}");

            foreach (var filePath in SafeEnumerateXml(target.ScanPath))
            {
                summary.Total++;
                try
                {
                    ProcessFile(filePath, target.DriveRoot, summary);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    Console.Error.WriteLine($"[ERROR] {filePath}: {ex.Message}");
                }
            }
        }

        return summary;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ProcessFile(string filePath, string driveRoot, ScanSummary summary)
    {
        var rawBytes = File.ReadAllBytes(filePath);
        var format   = XmlIdentifier.Detect(rawBytes);

        if (format == XmlFormat.Unknown)
        {
            summary.Skipped++;
            Console.WriteLine($"[SKIP]  {filePath} — ไม่ใช่ MSI XML");
            return;
        }

        bool   isEncrypted = format == XmlFormat.Encrypted;
        byte[] plaintextBytes;

        if (isEncrypted && _decryptor is not null)
        {
            // ถอดรหัสใน memory เพื่ออ่าน fields — ไม่เขียนไฟล์ถอดรหัสทิ้ง
            plaintextBytes = _decryptor.DecryptXml(rawBytes);
        }
        else
        {
            // plain XML หรือ encrypted แต่ไม่มี key → ใช้ raw bytes
            // (encrypted ไม่มี key: fields จะเป็น null แต่ยังคัดลอกและบันทึก DB ได้)
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
        CopyFile(filePath, driveRoot, rawBytes);

        summary.Collected++;
        var tag = isEncrypted
            ? (_decryptor is not null ? "ENC" : "ENC-NO-KEY")
            : "PLAIN";
        Console.WriteLine($"[OK]    {filePath} [{tag}] {fields?.SurveyJobNo ?? "-"}");
    }

    private void CopyFile(string sourcePath, string driveRoot, byte[] rawBytes)
    {
        var relative = Path.GetRelativePath(driveRoot, sourcePath);
        var dest     = Path.Combine(_collectRoot, _machineName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.WriteAllBytes(dest, rawBytes);
    }

    private static string ComputeMd5(byte[] data)
        => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();

    /// <summary>
    /// Enumerate *.xml แบบ recursive — ข้ามโฟลเดอร์ที่ access denied โดยไม่หยุด
    /// </summary>
    private static IEnumerable<string> SafeEnumerateXml(string root)
    {
        var pending = new Queue<string>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            var dir = pending.Dequeue();

            IEnumerable<string> files;
            try   { files = Directory.EnumerateFiles(dir, "*.xml"); }
            catch { continue; }

            foreach (var f in files) yield return f;

            IEnumerable<string> subdirs;
            try   { subdirs = Directory.EnumerateDirectories(dir); }
            catch { continue; }

            foreach (var s in subdirs) pending.Enqueue(s);
        }
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
