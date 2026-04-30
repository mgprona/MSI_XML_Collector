using Scout.Crypto;
using Scout.Data;
using Scout.Logging;
using Scout.Scanner;

// Scout_XML.exe [--source <folder>] [--key <path>] [--db <path>] [--pause]

var exeDir = AppContext.BaseDirectory;
var options = ParseArgs(args, exeDir);

if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

if (options.Error is not null)
{
    Console.Error.WriteLine(options.Error);
    Console.Error.WriteLine();
    PrintHelp();
    PauseIfRequested(options.Pause);
    return 1;
}

using var log = RunLogger.Create(exeDir);

// --- Scan targets -----------------------------------------------------------
IReadOnlyList<ScanTarget> targets;

if (options.SourceDir is not null)
{
    if (!Directory.Exists(options.SourceDir))
    {
        log.Error($"ไม่พบโฟลเดอร์: {options.SourceDir}");
        PauseIfRequested(options.Pause);
        return 1;
    }

    targets = [new ScanTarget(DriveRoot: options.SourceDir, ScanPath: options.SourceDir)];
    log.Info("[INFO] Mode    : Manual (--source)");
}
else
{
    targets = DriveScanner.GetTargets(exeDir);
    log.Info($"[INFO] Mode    : Auto ({targets.Count} target(s))");
}

if (targets.Count == 0)
{
    log.Warn("[WARN] No scan targets found.");
    PauseIfRequested(options.Pause);
    return 1;
}

// --- DB setup ---------------------------------------------------------------
var connStr = DatabaseInitializer.BuildConnectionString(options.DbPath);
DatabaseInitializer.EnsureCreated(connStr);
var repo = new FileRecordRepository(connStr);

// --- AES key ----------------------------------------------------------------
AesDecryptor? decryptor = null;
if (File.Exists(options.KeyPath))
{
    try
    {
        decryptor = AesDecryptor.LoadFromFile(options.KeyPath);
        log.Info($"[INFO] Key     : {options.KeyPath}");
    }
    catch (Exception ex)
    {
        log.Warn($"[WARN] โหลด key ไม่ได้ ({options.KeyPath}): {ex.Message}");
        log.Warn("[WARN] ไฟล์เข้ารหัสจะ error — ไฟล์ plain จะยังทำงานได้ปกติ");
    }
}
else
{
    log.Info("[INFO] ไม่พบ secrets.key — ไฟล์เข้ารหัสจะบันทึก DB โดยไม่มี fields (copy ไฟล์ได้ปกติ)");
}

// --- Print targets ----------------------------------------------------------
var collectRoot = Path.Combine(exeDir, "Collected_XMLs");
var machineName = Environment.MachineName;

log.Info($"[INFO] Machine : {machineName}");
log.Info($"[INFO] DB      : {options.DbPath}");
log.Info($"[INFO] Collect : {collectRoot}");
log.Info($"[INFO] Log     : {log.LogPath}");
log.Info("");

// --- Run --------------------------------------------------------------------
var scanner = new FileScanner(targets, collectRoot, machineName, decryptor, repo, log);
var summary = scanner.Run();

log.Info("");
log.Info($"Done — Total:{summary.Total}  New:{summary.Collected}  Dup:{summary.Duplicates}  Skip:{summary.Skipped}  Err:{summary.Errors}");

var exitCode = summary.Errors > 0 ? 2 : 0;
PauseIfRequested(options.Pause);
return exitCode;

static Options ParseArgs(string[] args, string exeDir)
{
    string? sourceDir = null;
    var keyPath = Path.Combine(exeDir, "secrets.key");
    var dbPath = Path.Combine(exeDir, "Master_Index.db");
    var pause = false;

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--help":
            case "-h":
            case "/?":
                return new Options(sourceDir, keyPath, dbPath, pause, ShowHelp: true);

            case "--pause":
                pause = true;
                break;

            case "--source":
                if (!TryReadValue(args, ref i, arg, out sourceDir, out var sourceError))
                    return Options.WithError(sourceError, pause);
                break;

            case "--key":
                if (!TryReadValue(args, ref i, arg, out keyPath, out var keyError))
                    return Options.WithError(keyError, pause);
                break;

            case "--db":
                if (!TryReadValue(args, ref i, arg, out dbPath, out var dbError))
                    return Options.WithError(dbError, pause);
                break;

            default:
                return Options.WithError($"Unknown argument: {arg}", pause);
        }
    }

    return new Options(sourceDir, keyPath, dbPath, pause);
}

static bool TryReadValue(string[] args, ref int index, string option, out string value, out string? error)
{
    value = "";
    error = null;

    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
    {
        error = $"Missing value for {option}";
        return false;
    }

    value = args[++index];
    return true;
}

static void PrintHelp()
{
    Console.WriteLine("""
MSI XML Scout

Usage:
  Scout_XML.exe [--source <folder>] [--key <path>] [--db <path>] [--pause]

Options:
  --source <folder>  Scan only this folder. If omitted, Scout runs Auto mode.
  --key <path>       secrets.key path. Default: secrets.key beside Scout_XML.exe.
  --db <path>        SQLite DB path. Default: Master_Index.db beside Scout_XML.exe.
  --pause            Wait for Enter before closing. Useful for double-click/batch use.
  --help             Show this help.

Auto mode:
  - C:\DOLCAD_XML
  - C:\Users\*\Desktop
  - C:\Users\*\Documents
  - Other fixed drives except the drive where Scout_XML.exe is running
""");
}

static void PauseIfRequested(bool pause)
{
    if (!pause) return;

    Console.WriteLine();
    Console.Write("Press Enter to close...");
    Console.ReadLine();
}

internal sealed record Options(
    string? SourceDir,
    string KeyPath,
    string DbPath,
    bool Pause,
    bool ShowHelp = false,
    string? Error = null)
{
    public static Options WithError(string? error, bool pause)
        => new(null, "", "", pause, Error: error ?? "Invalid arguments.");
}
