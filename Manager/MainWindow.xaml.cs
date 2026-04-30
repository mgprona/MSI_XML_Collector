using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Manager.Data;
using Manager.Models;
using Microsoft.Win32;

namespace Manager;

public partial class MainWindow : Window
{
    private List<FileRecord> _records = [];
    private ICollectionView? _view;
    private string? _currentDbPath;

    public MainWindow() => InitializeComponent();

    // ── Open / Load ──────────────────────────────────────────────────────────

    private void BtnOpenDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title    = "เปิด Master_Index.db",
            Filter   = "SQLite Database (*.db)|*.db|All files (*.*)|*.*",
            FileName = "Master_Index.db",
        };
        if (dlg.ShowDialog() == true)
            LoadDb(dlg.FileName);
    }

    private void LoadDb(string dbPath)
    {
        try
        {
            _currentDbPath = dbPath;
            _records = ManagerDb.LoadAll(dbPath);

            RefreshView();
            PopulateFilters();

            txtDbPath.Text = dbPath;
            txtStatus.Text = "โหลดสำเร็จ";
            UpdateDetail(null);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"โหลด DB ไม่สำเร็จ:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "โหลดไม่สำเร็จ";
        }
    }

    // ── View / Filter ─────────────────────────────────────────────────────────

    private void RefreshView()
    {
        _view = CollectionViewSource.GetDefaultView(_records);
        _view.Filter = FilterRecord;
        dgFiles.ItemsSource = _view;
        UpdateCount();
    }

    private bool FilterRecord(object obj)
    {
        var r = (FileRecord)obj;

        if (cbProvince.SelectedItem is string prov && prov != "(ทั้งหมด)" && r.ProvinceName != prov)
            return false;

        if (cbMachine.SelectedItem is string machine && machine != "(ทั้งหมด)" && r.MachineName != machine)
            return false;

        if (chkConflictOnly.IsChecked == true && !r.IsConflict)
            return false;

        var search = txtSearch.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var q = search.ToLowerInvariant();
            bool hit = r.SurveyJobNo?.ToLowerInvariant().Contains(q)  == true
                    || r.OwnerName?.ToLowerInvariant().Contains(q)     == true
                    || r.SurveyorName?.ToLowerInvariant().Contains(q)  == true
                    || r.OriginalFileName.ToLowerInvariant().Contains(q);
            if (!hit) return false;
        }

        return true;
    }

    private void ApplyFilter()
    {
        _view?.Refresh();
        UpdateCount();
    }

    private void PopulateFilters()
    {
        var savedProv    = cbProvince.SelectedItem as string;
        var savedMachine = cbMachine.SelectedItem as string;

        var provinces = _records
            .Select(r => r.ProvinceName ?? "")
            .Where(p => p.Length > 0)
            .Distinct().OrderBy(p => p)
            .Prepend("(ทั้งหมด)").ToList();

        var machines = _records
            .Select(r => r.MachineName)
            .Distinct().OrderBy(m => m)
            .Prepend("(ทั้งหมด)").ToList();

        cbProvince.ItemsSource  = provinces;
        cbProvince.SelectedItem = provinces.Contains(savedProv ?? "") ? savedProv : "(ทั้งหมด)";

        cbMachine.ItemsSource  = machines;
        cbMachine.SelectedItem = machines.Contains(savedMachine ?? "") ? savedMachine : "(ทั้งหมด)";
    }

    private void UpdateCount()
    {
        if (_view is null) return;
        var showing   = _view.Cast<FileRecord>().Count();
        var conflicts = _records.Count(r => r.IsConflict);
        txtCount.Text = $"แสดง {showing:N0} / {_records.Count:N0} รายการ   |   ซ้ำ {conflicts:N0} รายการ";
    }

    // ── Event handlers (filter controls) ────────────────────────────────────

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void Filter_TextChanged(object sender, TextChangedEventArgs e)            => ApplyFilter();
    private void Filter_CheckChanged(object sender, RoutedEventArgs e)                => ApplyFilter();

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        cbProvince.SelectedIndex  = 0;
        cbMachine.SelectedIndex   = 0;
        txtSearch.Text            = "";
        chkConflictOnly.IsChecked = false;
        ApplyFilter();
    }

    // ── DataGrid selection → Detail panel ───────────────────────────────────

    private void DgFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateDetail(dgFiles.SelectedItem as FileRecord);

    private void UpdateDetail(FileRecord? r)
    {
        if (r is null)
        {
            txtDetail.Text     = "";
            txtXmlContent.Text = "";
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"เลขที่สอบสวน : {r.SurveyJobNo ?? "-"}");
        sb.AppendLine($"ชื่อเจ้าของ  : {r.OwnerName ?? "-"}");
        sb.AppendLine($"วันที่       : {r.QueueDateDisplay}");
        sb.AppendLine($"จังหวัด      : {r.ProvinceName ?? "-"}");
        sb.AppendLine($"อำเภอ        : {r.AmphurSeq?.ToString() ?? "-"}   ตำบล : {r.TambolSeq?.ToString() ?? "-"}");
        sb.AppendLine($"เลขที่ดิน   : {r.LandNo?.ToString() ?? "-"}   เลขสำรวจ : {r.SurveyNo?.ToString() ?? "-"}");
        sb.AppendLine($"นักสำรวจ     : {r.SurveyorName ?? "-"}");
        sb.AppendLine();
        sb.AppendLine($"ไฟล์     : {r.OriginalFileName}");
        sb.AppendLine($"เครื่อง  : {r.MachineName}");
        sb.AppendLine($"รูปแบบ   : {r.EncryptedDisplay}");
        sb.AppendLine($"ขนาด     : {r.FileSizeDisplay}");
        sb.AppendLine($"เก็บเมื่อ : {r.CollectedAtDisplay}");
        sb.AppendLine($"Hash     : {r.FileHash}");
        if (r.IsConflict)
            sb.AppendLine("\n[!] เลขที่สอบสวนซ้ำกับไฟล์อื่น");

        txtDetail.Text     = sb.ToString();
        txtXmlContent.Text = TryReadXmlFile(r);
    }

    private string TryReadXmlFile(FileRecord r)
    {
        if (_currentDbPath is null) return "(ยังไม่ได้เปิด DB)";

        var dbDir      = Path.GetDirectoryName(_currentDbPath)!;
        var machineDir = Path.Combine(dbDir, "Collected_XMLs", r.MachineName);

        if (!Directory.Exists(machineDir))
            return $"(ไม่พบโฟลเดอร์ Collected_XMLs\\{r.MachineName})";

        var hits = Directory.GetFiles(machineDir, r.OriginalFileName, SearchOption.AllDirectories);

        if (hits.Length == 0)
            return $"(ไม่พบไฟล์ '{r.OriginalFileName}' ใน Collected_XMLs)";

        if (hits.Length > 1)
            return $"(พบ {hits.Length} ไฟล์ที่มีชื่อเดียวกัน — กรุณาตรวจสอบด้วยตนเอง)";

        try
        {
            var text = File.ReadAllText(hits[0], Encoding.UTF8);
            if (r.IsEncrypted)
                text = "[ไฟล์เข้ารหัส — เนื้อหาด้านล่างเป็น raw XML ก่อนถอดรหัส]\n\n" + text;
            return text;
        }
        catch (Exception ex)
        {
            return $"(อ่านไฟล์ไม่ได้: {ex.Message})";
        }
    }

    // ── Merge ────────────────────────────────────────────────────────────────

    private void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        // ถ้ายังไม่มี master DB → ให้เลือกที่บันทึกก่อน (ครั้งแรก)
        if (_currentDbPath is null)
        {
            var saveDlg = new SaveFileDialog
            {
                Title    = "สร้าง Master DB บนเครื่องนี้",
                Filter   = "SQLite Database (*.db)|*.db",
                FileName = "Master_Index.db",
            };
            if (saveDlg.ShowDialog() != true) return;
            _currentDbPath = saveDlg.FileName;
            txtDbPath.Text = _currentDbPath;
        }

        var dlg = new OpenFileDialog
        {
            Title    = "เลือก DB จาก USB เพื่อ Merge เข้า DB ปัจจุบัน",
            Filter   = "SQLite Database (*.db)|*.db|All files (*.*)|*.*",
            FileName = "Master_Index.db",
        };
        if (dlg.ShowDialog() != true) return;

        if (string.Equals(dlg.FileName, _currentDbPath, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("ไม่สามารถ merge DB กับตัวเองได้",
                "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            txtStatus.Text = "กำลัง merge DB...";
            var (inserted, updated) = ManagerDb.MergeFrom(dlg.FileName, _currentDbPath);

            txtStatus.Text = "กำลัง copy ไฟล์ XML...";
            var (copied, skipped) = ManagerDb.CopyCollectedFiles(dlg.FileName, _currentDbPath);

            // ถามลบ Collected_XMLs บน USB
            var deleteResult = MessageBox.Show(
                $"Merge สำเร็จ\n\n" +
                $"DB Records\n  เพิ่มใหม่  : {inserted:N0} รายการ\n  อัปเดต    : {updated:N0} รายการ\n\n" +
                $"ไฟล์ XML\n  copy ใหม่  : {copied:N0} ไฟล์\n  มีอยู่แล้ว : {skipped:N0} ไฟล์\n\n" +
                $"ต้องการลบไฟล์ XML ใน Collected_XMLs บน USB ด้วยไหม?\n(DB บน USB จะยังอยู่เพื่อป้องกัน collect ซ้ำรอบหน้า)",
                "ลบไฟล์ USB?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (deleteResult == MessageBoxResult.Yes)
            {
                int deleted = ManagerDb.DeleteCollectedFiles(dlg.FileName);
                MessageBox.Show($"ลบไฟล์ใน USB เรียบร้อย {deleted:N0} ไฟล์",
                    "ลบสำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            LoadDb(_currentDbPath);
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Merge ไม่สำเร็จ";
            MessageBox.Show($"Merge ไม่สำเร็จ:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
