namespace Scout.Scanner;

/// <summary>
/// DriveRoot = ฐานสำหรับคำนวณ relative path ไปยัง Collected_XMLs
/// ScanPath  = โฟลเดอร์ที่จะ enumerate ไฟล์ .xml จริงๆ
/// </summary>
public record ScanTarget(string DriveRoot, string ScanPath);
