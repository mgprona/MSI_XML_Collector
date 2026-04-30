using Scout.Crypto;
using Scout.Data;
using Scout.Scanner;

// ---------------------------------------------------------------------------
// Scout_XML.exe [--source <folder>] [--key <path>] [--db <path>]
//
// Auto mode (ไม่มี --source):
//   C:\  → สแกนเฉพาะ DOLCAD_XML, Users\*\Desktop, Users\*\Documents
//   D:\, E:\, ... (Fixed, ไม่ใช่ drive ที่ exe รันอยู่) → สแกนทั้ง drive
//
// Manual mode (มี --source):
//   สแกนเฉพาะโฟลเดอร์ที่ระบุ (ใช้สำหรับทดสอบ)
// ---------------------------------------------------------------------------

var exeDir  = AppContext.BaseDirectory;
string? sourceDir = null;
string  keyPath   = Path.Combine(exeDir, "secrets.key");
string  dbPath    = Path.Combine(exeDir, "Master_Index.db");

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--source" when i + 1 < args.Length: sourceDir = args[++i]; break;
        case "--key"    when i + 1 < args.Length: keyPath   = args[++i]; break;
        case "--db"     when i + 1 < args.Length: dbPath    = args[++i]; break;
    }
}

// --- Scan targets -----------------------------------------------------------
IReadOnlyList<ScanTarget> targets;

if (sourceDir is not null)
{
    if (!Directory.Exists(sourceDir))
    {
        Console.Error.WriteLine($"ไม่พบโฟลเดอร์: {sourceDir}");
        return 1;
    }
    targets = [new ScanTarget(DriveRoot: sourceDir, ScanPath: sourceDir)];
    Console.WriteLine($"[INFO] Mode    : Manual (--source)");
}
else
{
    targets = DriveScanner.GetTargets(exeDir);
    Console.WriteLine($"[INFO] Mode    : Auto ({targets.Count} target(s))");
}

// --- DB setup ---------------------------------------------------------------
var connStr = DatabaseInitializer.BuildConnectionString(dbPath);
DatabaseInitializer.EnsureCreated(connStr);
var repo = new FileRecordRepository(connStr);

// --- AES key ----------------------------------------------------------------
AesDecryptor? decryptor = null;
if (File.Exists(keyPath))
{
    try
    {
        decryptor = AesDecryptor.LoadFromFile(keyPath);
        Console.WriteLine($"[INFO] Key     : {keyPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WARN] โหลด key ไม่ได้ ({keyPath}): {ex.Message}");
        Console.Error.WriteLine("[WARN] ไฟล์เข้ารหัสจะ error — ไฟล์ plain จะยังทำงานได้ปกติ");
    }
}
else
{
    Console.WriteLine($"[INFO] ไม่พบ secrets.key — ไฟล์เข้ารหัสจะบันทึก DB โดยไม่มี fields (copy ไฟล์ได้ปกติ)");
}

// --- Print targets ----------------------------------------------------------
var collectRoot = Path.Combine(exeDir, "Collected_XMLs");
var machineName = Environment.MachineName;

Console.WriteLine($"[INFO] Machine : {machineName}");
Console.WriteLine($"[INFO] DB      : {dbPath}");
Console.WriteLine($"[INFO] Collect : {collectRoot}");
Console.WriteLine();

// --- Run --------------------------------------------------------------------
var scanner = new FileScanner(targets, collectRoot, machineName, decryptor, repo);
var summary = scanner.Run();

Console.WriteLine();
Console.WriteLine($"Done — Total:{summary.Total}  New:{summary.Collected}  Dup:{summary.Duplicates}  Skip:{summary.Skipped}  Err:{summary.Errors}");
return summary.Errors > 0 ? 2 : 0;
