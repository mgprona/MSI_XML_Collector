using Microsoft.Data.Sqlite;
using Scout.Models;

namespace Scout.Data;

public class FileRecordRepository
{
    private readonly string _connectionString;

    public FileRecordRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public long Upsert(FileRecord r)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
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
    SurveyorName     = excluded.SurveyorName
RETURNING Id;";

        cmd.Parameters.AddWithValue("$hash",     r.FileHash);
        cmd.Parameters.AddWithValue("$name",     r.OriginalFileName);
        cmd.Parameters.AddWithValue("$path",     r.OriginalPath);
        cmd.Parameters.AddWithValue("$machine",  r.MachineName);
        cmd.Parameters.AddWithValue("$size",     r.FileSize);
        cmd.Parameters.AddWithValue("$enc",      r.IsEncrypted ? 1 : 0);
        cmd.Parameters.AddWithValue("$lwt",      r.LastWriteTime.ToString("O"));
        cmd.Parameters.AddWithValue("$col",      (r.CollectedAt == default ? DateTime.UtcNow : r.CollectedAt).ToString("O"));
        cmd.Parameters.AddWithValue("$job",      (object?)r.SurveyJobNo  ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$owner",    (object?)r.OwnerName    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$qd",       r.QueueDate.HasValue ? r.QueueDate.Value.ToString("O") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$prov",     (object?)r.ProvinceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$amphur",   (object?)r.AmphurSeq   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tambol",   (object?)r.TambolSeq   ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$land",     (object?)r.LandNo      ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$survey",   (object?)r.SurveyNo    ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$surveyor", (object?)r.SurveyorName ?? DBNull.Value);

        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public bool ExistsByHash(string hash, string machineName)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Files WHERE FileHash=$h AND MachineName=$m LIMIT 1;";
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$m", machineName);
        return cmd.ExecuteScalar() is not null;
    }
}
