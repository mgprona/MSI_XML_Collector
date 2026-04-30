using Microsoft.Data.Sqlite;

namespace Scout.Data;

public static class DatabaseInitializer
{
    public const string DefaultDbFileName = "MSI_XML_Collector.db";

    public static string BuildConnectionString(string dbPath)
        => new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

    public static void EnsureCreated(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();

        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = Schema;
        cmd.ExecuteNonQuery();
    }

    private const string Schema = @"
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

CREATE UNIQUE INDEX IF NOT EXISTS UX_Files_Hash_Machine ON Files(FileHash, MachineName);
CREATE INDEX IF NOT EXISTS IX_Files_SurveyJobNo  ON Files(SurveyJobNo);
CREATE INDEX IF NOT EXISTS IX_Files_OwnerName    ON Files(OwnerName);
CREATE INDEX IF NOT EXISTS IX_Files_ProvinceName ON Files(ProvinceName);
CREATE INDEX IF NOT EXISTS IX_Files_QueueDate    ON Files(QueueDate);
CREATE INDEX IF NOT EXISTS IX_Files_SurveyorName ON Files(SurveyorName);
";
}
