using System.Diagnostics;
using System.Security.Cryptography;
using Scout.Crypto;
using Scout.Data;
using Scout.Logging;
using Scout.Models;
using Scout.Xml;

namespace Scout.Scanner;

public class FileScanner
{
    private static readonly HashSet<string> SkipDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin",
        "$WinREAgent",
        "Config.Msi",
        "Documents and Settings",
        "MSOCache",
        "OneDriveTemp",
        "PerfLogs",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "Recovery",
        "System Volume Information",
        "Windows",
        "Windows.old",
        "Windows10Upgrade",
    };

    private readonly IReadOnlyList<ScanTarget> _targets;
    private readonly string _collectRoot;
    private readonly string _machineName;
    private readonly AesDecryptor? _decryptor;
    private readonly FileRecordRepository _repo;
    private readonly RunLogger _log;

    public FileScanner(
        IReadOnlyList<ScanTarget> targets,
        string collectRoot,
        string machineName,
        AesDecryptor? decryptor,
        FileRecordRepository repo,
        RunLogger log)
    {
        _targets     = targets;
        _collectRoot = collectRoot;
        _machineName = machineName;
        _decryptor   = decryptor;
        _repo        = repo;
        _log         = log;
    }

    public ScanSummary Run()
    {
        var summary = new ScanSummary();
        var started = Stopwatch.StartNew();

        foreach (var target in _targets)
        {
            if (!Directory.Exists(target.ScanPath))
            {
                _log.Info($"[SKIP]  {target.ScanPath} — ไม่พบโฟลเดอร์");
                continue;
            }

            _log.Info($"[SCAN]  {target.ScanPath}");
            var targetStartTotal = summary.Total;

            foreach (var filePath in SafeEnumerateXml(target.ScanPath, _log))
            {
                summary.Total++;
                try
                {
                    ProcessFile(filePath, target.DriveRoot, summary);
                }
                catch (Exception ex)
                {
                    summary.Errors++;
                    _log.Error($"[ERROR] {filePath}: {ex.Message}");
                }

                if (summary.Total % 100 == 0)
                    _log.Info($"[PROGRESS] Total:{summary.Total}  New:{summary.Collected}  Dup:{summary.Duplicates}  Skip:{summary.Skipped}  Err:{summary.Errors}  Elapsed:{started.Elapsed:hh\\:mm\\:ss}");
            }

            var foundInTarget = summary.Total - targetStartTotal;
            _log.Info($"[DONE]  {target.ScanPath} — {foundInTarget:N0} XML file(s)");
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
            _log.Info($"[SKIP]  {filePath} — ไม่ใช่ MSI XML");
            return;
        }

        bool   isEncrypted = format == XmlFormat.Encrypted;
        byte[] plaintextBytes;

        if (isEncrypted && _decryptor is not null)
        {
            // ถอดรหัสใน memory เพื่ออ่าน fields — ไม่เขียนไฟล์ถอดรหัสทิ้ง
            plaintextBytes = _decryptor.DecryptXml(rawBytes);
        }
        else if (isEncrypted)
        {
            summary.Skipped++;
            _log.Warn($"[SKIP]  {filePath} — encrypted XML แต่ไม่มี secrets.key จึงยืนยันว่าเป็น DOLCAD survey XML ไม่ได้");
            return;
        }
        else
        {
            plaintextBytes = rawBytes;
        }

        var fields = XmlParser.Parse(plaintextBytes);
        if (fields?.SurveyJobNo is null)
        {
            summary.Skipped++;
            _log.Info($"[SKIP]  {filePath} — ไม่พบ TB_SVC_SURVEYDESC/SURVEYJOB_NO ของ DOLCAD");
            return;
        }

        var hash = ComputeMd5(plaintextBytes);

        if (_repo.ExistsByHash(hash, _machineName))
        {
            summary.Duplicates++;
            _log.Info($"[DUP]   {filePath}");
            return;
        }

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
        _log.Info($"[OK]    {filePath} [{tag}] {fields?.SurveyJobNo ?? "-"}");
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
    private static IEnumerable<string> SafeEnumerateXml(string root, RunLogger log)
    {
        var pending = new Queue<string>();
        pending.Enqueue(root);
        var scannedDirs = 0;
        var lastHeartbeat = Stopwatch.StartNew();

        while (pending.Count > 0)
        {
            var dir = pending.Dequeue();
            scannedDirs++;

            if (lastHeartbeat.Elapsed >= TimeSpan.FromSeconds(15))
            {
                log.Info($"[SEARCH] scanned {scannedDirs:N0} folder(s), pending {pending.Count:N0}; current: {dir}");
                lastHeartbeat.Restart();
            }

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir, "*.xml"); }
            catch (UnauthorizedAccessException ex)
            {
                log.Warn($"[WARN]  skip folder (access denied): {dir} ({ex.Message})");
                continue;
            }
            catch (Exception ex)
            {
                log.Warn($"[WARN]  skip folder: {dir} ({ex.Message})");
                continue;
            }

            foreach (var f in files) yield return f;

            IEnumerable<string> subdirs;
            try { subdirs = Directory.EnumerateDirectories(dir); }
            catch (UnauthorizedAccessException ex)
            {
                log.Warn($"[WARN]  cannot list subfolders (access denied): {dir} ({ex.Message})");
                continue;
            }
            catch (Exception ex)
            {
                log.Warn($"[WARN]  cannot list subfolders: {dir} ({ex.Message})");
                continue;
            }

            foreach (var s in subdirs)
            {
                if (ShouldSkipDirectory(s))
                {
                    log.Info($"[SKIPDIR] {s} — system/backup folder");
                    continue;
                }

                pending.Enqueue(s);
            }
        }
    }

    private static bool ShouldSkipDirectory(string path)
        => SkipDirectoryNames.Contains(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
}

public class ScanSummary
{
    public int Total      { get; set; }
    public int Collected  { get; set; }
    public int Duplicates { get; set; }
    public int Skipped    { get; set; }
    public int Errors     { get; set; }
}
