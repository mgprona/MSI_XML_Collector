using Scout.Crypto;
using Scout.Data;
using Scout.Scanner;

// ---------------------------------------------------------------------------
// CLI: Scout_XML.exe --source <folder> [--key <secrets.key>] [--db <path>]
//      Defaults: --key and --db next to the exe; --source required.
// ---------------------------------------------------------------------------

var exeDir = AppContext.BaseDirectory;

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

if (sourceDir is null)
{
    Console.Error.WriteLine("Usage: Scout_XML.exe --source <folder> [--key <path>] [--db <path>]");
    return 1;
}

if (!Directory.Exists(sourceDir))
{
    Console.Error.WriteLine($"Source folder not found: {sourceDir}");
    return 1;
}

// --- DB setup ---------------------------------------------------------------
var connStr = DatabaseInitializer.BuildConnectionString(dbPath);
DatabaseInitializer.EnsureCreated(connStr);
var repo = new FileRecordRepository(connStr);

// --- AES key (optional — only needed if encrypted files exist) --------------
AesDecryptor? decryptor = null;
if (File.Exists(keyPath))
{
    try
    {
        decryptor = AesDecryptor.LoadFromFile(keyPath);
        Console.WriteLine($"[INFO] Key loaded: {keyPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WARN] Could not load key ({keyPath}): {ex.Message}");
        Console.Error.WriteLine("[WARN] Encrypted files will fail — plaintext files will still be processed.");
    }
}
else
{
    Console.WriteLine($"[WARN] secrets.key not found at {keyPath} — encrypted files will be skipped with an error.");
}

// --- Scan -------------------------------------------------------------------
var collectRoot = Path.Combine(exeDir, "Collected_XMLs");
var machineName = Environment.MachineName;

Console.WriteLine($"[INFO] Source  : {sourceDir}");
Console.WriteLine($"[INFO] Collect : {collectRoot}");
Console.WriteLine($"[INFO] DB      : {dbPath}");
Console.WriteLine($"[INFO] Machine : {machineName}");
Console.WriteLine();

var scanner = new FileScanner(sourceDir, collectRoot, machineName, decryptor, repo);
var summary = scanner.Run();

Console.WriteLine();
Console.WriteLine($"Done — Total:{summary.Total}  New:{summary.Collected}  Dup:{summary.Duplicates}  Skip:{summary.Skipped}  Err:{summary.Errors}");
return summary.Errors > 0 ? 2 : 0;
