using System;

namespace Scout.Models;

public class FileRecord
{
    public long Id { get; set; }
    public string FileHash { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string MachineName { get; set; } = "";
    public long FileSize { get; set; }
    public bool IsEncrypted { get; set; }
    public DateTime LastWriteTime { get; set; }
    public DateTime CollectedAt { get; set; }

    // Parsed from TB_SVC_SURVEYDESC
    public string? SurveyJobNo { get; set; }
    public string? OwnerName { get; set; }
    public DateTime? QueueDate { get; set; }
    public string? ProvinceName { get; set; }
    public int? AmphurSeq { get; set; }
    public int? TambolSeq { get; set; }
    public int? LandNo { get; set; }
    public int? SurveyNo { get; set; }
    public string? SurveyorName { get; set; }
}
