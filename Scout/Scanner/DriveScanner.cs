namespace Scout.Scanner;

public static class DriveScanner
{
    // โฟลเดอร์ใน C: ที่ข้ามได้เลย — system profiles
    private static readonly HashSet<string> SkipUserProfiles = new(StringComparer.OrdinalIgnoreCase)
        { "Public", "Default", "Default User", "All Users" };

    /// <summary>
    /// สร้าง scan targets อัตโนมัติ:
    ///   C:\ → เฉพาะ DOLCAD_XML, Users\*\Desktop, Users\*\Documents
    ///   Drive อื่น (Fixed, ไม่ใช่ drive ที่ exe รันอยู่) → สแกนทั้ง drive
    /// </summary>
    public static List<ScanTarget> GetTargets(string exeDir)
    {
        var exeDrive = (Path.GetPathRoot(exeDir) ?? "").TrimEnd('\\').ToUpperInvariant();
        var targets  = new List<ScanTarget>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;

            var root      = drive.RootDirectory.FullName;                    // e.g. "C:\"
            var driveLetter = root.TrimEnd('\\').ToUpperInvariant();          // e.g. "C:"

            // ข้าม drive ที่ Scout รันอยู่ (USB นั้นเอง)
            if (driveLetter == exeDrive) continue;

            if (driveLetter == "C:")
            {
                targets.AddRange(CDriveTargets());
            }
            else if (drive.DriveType == DriveType.Fixed)
            {
                // D:, E:, F:, ... — สแกนทั้ง drive
                targets.Add(new ScanTarget(DriveRoot: root, ScanPath: root));
            }
            // ข้าม: CD-ROM, Network, Removable อื่นๆ
        }

        return targets;
    }

    private static IEnumerable<ScanTarget> CDriveTargets()
    {
        const string root = @"C:\";

        // 1. โฟลเดอร์หลักของ DOLCAD
        yield return new ScanTarget(root, @"C:\DOLCAD_XML");

        // 2. Desktop และ Documents ของแต่ละ user (ข้าม system profiles)
        var usersDir = @"C:\Users";
        if (!Directory.Exists(usersDir)) yield break;

        foreach (var userDir in Directory.GetDirectories(usersDir))
        {
            if (SkipUserProfiles.Contains(Path.GetFileName(userDir))) continue;

            yield return new ScanTarget(root, Path.Combine(userDir, "Desktop"));
            yield return new ScanTarget(root, Path.Combine(userDir, "Documents"));
        }
    }
}
