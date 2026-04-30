using System.IO;
using Manager.Models;
using Microsoft.Data.Sqlite;

namespace Manager.Data;

public static class ManagerDb
{
    private static string BuildConnectionString(string dbPath, bool readOnly = false)
        => new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode       = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Cache      = SqliteCacheMode.Shared,
        }.ToString();

    public static List<FileRecord> LoadAll(string dbPath)
    {
        var records = new List<FileRecord>();
        using var conn = new SqliteConnection(BuildConnectionString(dbPath, readOnly: true));
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Files ORDER BY QueueDate DESC, CollectedAt DESC;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            records.Add(MapRow(reader));

        // Mark rows whose SurveyJobNo appears more than once
        var conflictJobs = records
            .Where(r => r.SurveyJobNo != null)
            .GroupBy(r => r.SurveyJobNo)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key!)
            .ToHashSet();

        foreach (var r in records)
            r.IsConflict = r.SurveyJobNo != null && conflictJobs.Contains(r.SurveyJobNo);

        return records;
    }

    /// <summary>
    /// Copy ไฟล์ทั้งหมดจาก Collected_XMLs ข้าง source DB มายัง Collected_XMLs ข้าง target DB
    /// ไฟล์ที่มีอยู่แล้วจะข้าม (ไม่ overwrite)
    /// </summary>
    public static (int Copied, int Skipped) CopyCollectedFiles(
        string sourceDbPath,
        string targetDbPath,
        IProgress<string>? progress = null)
    {
        var srcRoot = Path.Combine(Path.GetDirectoryName(sourceDbPath)!, "Collected_XMLs");
        var dstRoot = Path.Combine(Path.GetDirectoryName(targetDbPath)!, "Collected_XMLs");

        if (!Directory.Exists(srcRoot)) return (0, 0);

        int copied = 0, skipped = 0;

        foreach (var srcFile in Directory.EnumerateFiles(srcRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(srcRoot, srcFile);
            var dstFile  = Path.Combine(dstRoot, relative);

            if (File.Exists(dstFile)) { skipped++; continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);
            File.Copy(srcFile, dstFile);
            copied++;

            if ((copied + skipped) % 100 == 0)
                progress?.Report($"กำลัง copy ไฟล์ XML... copy {copied:N0}, ข้าม {skipped:N0}");
        }

        return (copied, skipped);
    }

    /// <summary>
    /// ลบไฟล์ทั้งหมดใน Collected_XMLs ข้าง source DB (เก็บ DB ไว้)
    /// คืนค่าจำนวนไฟล์ที่ลบ
    /// </summary>
    public static int DeleteCollectedFiles(string sourceDbPath, IProgress<string>? progress = null)
    {
        var srcRoot = Path.Combine(Path.GetDirectoryName(sourceDbPath)!, "Collected_XMLs");
        if (!Directory.Exists(srcRoot)) return 0;

        int deleted = 0;
        foreach (var subDir in Directory.GetDirectories(srcRoot))
        {
            deleted += Directory.GetFiles(subDir, "*", SearchOption.AllDirectories).Length;
            progress?.Report($"กำลังลบไฟล์บน USB... {deleted:N0} ไฟล์");
            Directory.Delete(subDir, recursive: true);
        }
        return deleted;
    }

    public static (int Inserted, int Updated) MergeFrom(
        string sourceDbPath,
        string targetDbPath,
        IProgress<string>? progress = null)
    {
        var sourceRecords = LoadAll(sourceDbPath);
        int inserted = 0, updated = 0;

        using var conn = new SqliteConnection(BuildConnectionString(targetDbPath));
        conn.Open();
        EnsureSchema(conn);

        for (var i = 0; i < sourceRecords.Count; i++)
        {
            var r = sourceRecords[i];
            bool exists = ExistsByHash(conn, r.FileHash, r.MachineName);
            Upsert(conn, r);
            if (exists) updated++; else inserted++;

            if ((i + 1) % 100 == 0)
                progress?.Report($"กำลัง merge DB... {i + 1:N0} / {sourceRecords.Count:N0} รายการ");
        }

        return (inserted, updated);
    }

    // -----------------------------------------------------------------------

    private static bool ExistsByHash(SqliteConnection conn, string hash, string machine)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Files WHERE FileHash=$h AND MachineName=$m LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$m", machine);
        return cmd.ExecuteScalar() is not null;
    }

    private static void Upsert(SqliteConnection conn, FileRecord r)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Files
    (FileHash, OriginalFileName, OriginalPath, MachineName, FileSize, IsEncrypted,
     LastWriteTime, CollectedAt,
     SurveyJobNo, OwnerName, QueueDate, ProvinceName,
     AmphurSeq, TambolSeq, LandNo, SurveyNo, SurveyorName)
VALUES
    ($hash, $name, $path, $machine, $size, $enc, $lwt, $col,
     $job, $owner, $qd, $prov, $amphur, $tambol, $land, $survey, $surveyor)
ON CONFLICT(FileHash, MachineName) DO UPDATE SET
    OriginalFileName = excluded.OriginalFileName,
    OriginalPath     = excluded.OriginalPath,
    FileSize         = excluded.FileSize,
    IsEncrypted      = excluded.IsEncrypted,
    LastWriteTime    = excluded.LastWriteTime,
    CollectedAt      = excluded.CollectedAt,
    SurveyJobNo      = excluded.SurveyJobNo,
    OwnerName        = excluded.OwnerName,
    QueueDate        = excluded.QueueDate,
    ProvinceName     = excluded.ProvinceName,
    AmphurSeq        = excluded.AmphurSeq,
    TambolSeq        = excluded.TambolSeq,
    LandNo           = excluded.LandNo,
    SurveyNo         = excluded.SurveyNo,
    SurveyorName     = excluded.SurveyorName;";

        cmd.Parameters.AddWithValue("$hash",     r.FileHash);
        cmd.Parameters.AddWithValue("$name",     r.OriginalFileName);
        cmd.Parameters.AddWithValue("$path",     r.OriginalPath);
        cmd.Parameters.AddWithValue("$machine",  r.MachineName);
        cmd.Parameters.AddWithValue("$size",     r.FileSize);
        cmd.Parameters.AddWithValue("$enc",      r.IsEncrypted ? 1 : 0);
        cmd.Parameters.AddWithValue("$lwt",      r.LastWriteTime.ToString("O"));
        cmd.Parameters.AddWithValue("$col",      r.CollectedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$job",      (object?)r.SurveyJobNo   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$owner",    (object?)r.OwnerName     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$qd",       r.QueueDate.HasValue ? r.QueueDate.Value.ToString("O") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$prov",     (object?)r.ProvinceName  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$amphur",   (object?)r.AmphurSeq    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tambol",   (object?)r.TambolSeq    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$land",     (object?)r.LandNo       ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$survey",   (object?)r.SurveyNo     ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$surveyor", (object?)r.SurveyorName ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    private static FileRecord MapRow(SqliteDataReader r)
    {
        static string? Str(SqliteDataReader rd, string col)
            => rd.IsDBNull(rd.GetOrdinal(col)) ? null : rd.GetString(rd.GetOrdinal(col));
        static int? Int(SqliteDataReader rd, string col)
            => rd.IsDBNull(rd.GetOrdinal(col)) ? null : rd.GetInt32(rd.GetOrdinal(col));
        static DateTime? DateN(SqliteDataReader rd, string col)
            => rd.IsDBNull(rd.GetOrdinal(col)) ? null : DateTime.Parse(rd.GetString(rd.GetOrdinal(col)));

        return new FileRecord
        {
            Id               = r.GetInt64(r.GetOrdinal("Id")),
            FileHash         = r.GetString(r.GetOrdinal("FileHash")),
            OriginalFileName = r.GetString(r.GetOrdinal("OriginalFileName")),
            OriginalPath     = r.GetString(r.GetOrdinal("OriginalPath")),
            MachineName      = r.GetString(r.GetOrdinal("MachineName")),
            FileSize         = r.GetInt64(r.GetOrdinal("FileSize")),
            IsEncrypted      = r.GetInt32(r.GetOrdinal("IsEncrypted")) == 1,
            LastWriteTime    = DateTime.Parse(r.GetString(r.GetOrdinal("LastWriteTime"))),
            CollectedAt      = DateTime.Parse(r.GetString(r.GetOrdinal("CollectedAt"))),
            SurveyJobNo      = Str(r, "SurveyJobNo"),
            OwnerName        = Str(r, "OwnerName"),
            QueueDate        = DateN(r, "QueueDate"),
            ProvinceName     = Str(r, "ProvinceName"),
            AmphurSeq        = Int(r, "AmphurSeq"),
            TambolSeq        = Int(r, "TambolSeq"),
            LandNo           = Int(r, "LandNo"),
            SurveyNo         = Int(r, "SurveyNo"),
            SurveyorName     = Str(r, "SurveyorName"),
        };
    }

    private static void EnsureSchema(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Files (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    FileHash          TEXT    NOT NULL,
    OriginalFileName  TEXT    NOT NULL,
    OriginalPath      TEXT    NOT NULL,
    MachineName       TEXT    NOT NULL,
    FileSize          INTEGER NOT NULL,
    IsEncrypted       INTEGER NOT NULL DEFAULT 0,
    LastWriteTime     TEXT    NOT NULL,
    CollectedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
    SurveyJobNo       TEXT,
    OwnerName         TEXT,
    QueueDate         TEXT,
    ProvinceName      TEXT,
    AmphurSeq         INTEGER,
    TambolSeq         INTEGER,
    LandNo            INTEGER,
    SurveyNo          INTEGER,
    SurveyorName      TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Files_Hash_Machine ON Files(FileHash, MachineName);";
        cmd.ExecuteNonQuery();
    }
}
