// Smart Folder seventh tab: no AI, no network; user approves all file moves.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DonDep
{
    partial class MainForm
    {
        string smartLastFolder;
        SmartResult smartResult;
        TextBox txtSmartFolder;
        ListView smartList;
        Label lblSmartCount, lblSmartHelp;
        RadioButton radioSmartPersonal, radioSmartWork;
        CheckBox chkSmartContent, chkSmartDate, chkSmartDup;
        Button btnSmartScan, btnSmartApply, btnSmartUndo, btnSmartSelect;
        bool smartLoading;

        void BuildSmartFolderTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(10) };
            top.Controls.Add(Lbl("Sắp xếp file cá nhân và tài liệu công việc - chạy ngoại tuyến, không cần AI", 18, 10, 1050, 24));
            top.Controls.Add(Lbl("Thư mục nguồn:", 18, 41, 112, 23));
            txtSmartFolder = new TextBox { Location = new Point(130, 39), Size = new Size(700, 26),
                ReadOnly = true, Text = smartLastFolder ?? Fs.Downloads };
            var btnBrowse = Btn("Chọn thư mục…", 842, 37, 155, 30);
            top.Controls.AddRange(new Control[] { txtSmartFolder, btnBrowse });
            radioSmartPersonal = new RadioButton { Text = "Cá nhân (theo định dạng)", Location = new Point(19, 77),
                Width = 235, Checked = true };
            radioSmartWork = new RadioButton { Text = "Công việc (theo chủ đề)", Location = new Point(265, 77), Width = 250 };
            top.Controls.AddRange(new Control[] { radioSmartPersonal, radioSmartWork });
            chkSmartContent = new CheckBox { Text = "Đọc nội dung Word/Excel/PowerPoint/TXT khi phân loại công việc",
                Location = new Point(19, 107), Width = 490, Checked = true };
            chkSmartDate = new CheckBox { Text = "Chia theo năm/tháng", Location = new Point(530, 107), Width = 175, Checked = true };
            chkSmartDup = new CheckBox { Text = "Phát hiện file trùng (SHA-256)", Location = new Point(723, 107), Width = 260, Checked = true };
            top.Controls.AddRange(new Control[] { chkSmartContent, chkSmartDate, chkSmartDup });
            btnSmartScan = Btn("Quét & đề xuất", 19, 145, 190, 36);
            btnSmartApply = Btn("Áp dụng file đã chọn", 222, 145, 240, 36);
            btnSmartApply.BackColor = Yellow;
            btnSmartUndo = Btn("Hoàn tác lần gần nhất", 476, 145, 235, 36);
            var btnOpen = Btn("Mở thư mục", 725, 145, 165, 36);
            btnSmartSelect = Btn("Chọn đề xuất", 902, 145, 170, 36);
            top.Controls.AddRange(new Control[]{btnSmartScan,btnSmartApply,btnSmartUndo,btnOpen,btnSmartSelect});
            tab.Controls.Add(top);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 84 };
            lblSmartCount = Lbl("", 18, 7, 1070, 26);
            lblSmartHelp = Lbl("Chỉ chuyển file sau khi xác nhận. Không xóa file. File chưa rõ/trùng được giữ nguyên. Hoàn tác từ nhật ký.",
                18, 37, 1070, 40);
            bottom.Controls.AddRange(new Control[]{lblSmartCount,lblSmartHelp});
            tab.Controls.Add(bottom);

            smartList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
                CheckBoxes = true, HideSelection = false, GridLines = true };
            smartList.Columns.Add("File nguồn", 230);
            smartList.Columns.Add("Nhóm đề xuất", 160);
            smartList.Columns.Add("Dung lượng", 95);
            smartList.Columns.Add("Lý do", 245);
            smartList.Columns.Add("Đường dẫn mới (xem trước)", 380);
            tab.Controls.Add(smartList);
            top.BringToFront();
            bottom.BringToFront();

            foreach(Control c in new Control[] { btnBrowse, radioSmartPersonal, radioSmartWork,
                chkSmartContent, chkSmartDate, chkSmartDup, btnOpen }) busyControls.Add(c);
            btnBrowse.Click += (s,e) => {
                using(var dlg = new FolderBrowserDialog { Description = "Chọn thư mục cá nhân / công việc muốn sắp xếp",
                    SelectedPath = Directory.Exists(txtSmartFolder.Text) ? txtSmartFolder.Text : Fs.Downloads,
                    ShowNewFolderButton = false })
                    if(dlg.ShowDialog(this)==DialogResult.OK) {
                        txtSmartFolder.Text = dlg.SelectedPath; smartLastFolder = dlg.SelectedPath; ResetSmartResults();
                    }
            };
            radioSmartPersonal.CheckedChanged += (s,e) => { if(radioSmartPersonal.Checked) ResetSmartResults(); };
            radioSmartWork.CheckedChanged += (s,e) => { if(radioSmartWork.Checked) ResetSmartResults(); };
            chkSmartContent.CheckedChanged += (s,e) => ResetSmartResults();
            chkSmartDate.CheckedChanged += (s,e) => ResetSmartResults();
            chkSmartDup.CheckedChanged += (s,e) => ResetSmartResults();
            smartList.ItemCheck += (s,e) => {
                if(smartList.Items[e.Index].Tag is SmartPlan p && !p.Suggested)
                    e.NewValue = CheckState.Unchecked;
            };
            smartList.ItemChecked += (s,e) => { if (!smartLoading) UpdateSmartSummary(); };
            smartList.DoubleClick += (s,e) => {
                if(smartList.SelectedItems.Count > 0)
                    Fs.OpenInExplorer(((SmartPlan)smartList.SelectedItems[0].Tag).Source);
            };
            btnSmartSelect.Click += (s,e) => {
                if(smartResult == null) return;
                bool select = smartList.Items.Cast<ListViewItem>()
                    .Any(i => ((SmartPlan)i.Tag).Suggested && !i.Checked);
                if(select && SmartFolders.IsDriveRoot(txtSmartFolder.Text) &&
                    !Confirm(UiLanguage.English ?
                        "Select all suggested files on the entire data drive? Review the destinations before moving." :
                        "Chọn tất cả file được đề xuất trên toàn bộ ổ dữ liệu? Hãy xem kỹ đường dẫn đích trước khi chuyển."))
                    return;
                smartLoading = true;
                smartList.BeginUpdate();
                try {
                    foreach(ListViewItem i in smartList.Items)
                        i.Checked = select && ((SmartPlan)i.Tag).Suggested;
                } finally { smartList.EndUpdate(); smartLoading = false; }
                UpdateSmartSummary();
            };
            btnSmartScan.Click += async (s,e) => await Guard(ScanSmartAsync);
            btnSmartApply.Click += async (s,e) => await Guard(ApplySmartAsync);
            btnSmartUndo.Click += async (s,e) => await Guard(UndoSmartAsync);
            btnOpen.Click += (s,e) => {
                if(Directory.Exists(txtSmartFolder.Text)) Fs.OpenInExplorer(txtSmartFolder.Text);
            };
            ResetSmartResults();
        }
        string SmartGroupName(string group)
        {
            var names = new Dictionary<string, string> {
                { "01 - Hinh anh", "01 - Hình ảnh" }, { "02 - Video", "02 - Video" },
                { "03 - Am thanh", "03 - Âm thanh" }, { "04 - Tai lieu", "04 - Tài liệu" },
                { "05 - Bang tinh", "05 - Bảng tính" }, { "06 - Trinh chieu", "06 - Trình chiếu" },
                { "07 - File nen", "07 - File nén" }, { "08 - Bo cai dat", "08 - Bộ cài đặt" },
                { "01 - Cong van", "01 - Công văn" }, { "02 - Quyet dinh", "02 - Quyết định" },
                { "03 - Ke hoach", "03 - Kế hoạch" }, { "04 - Bao cao", "04 - Báo cáo" },
                { "05 - Hop dong", "05 - Hợp đồng" }, { "06 - Hoa don Chung tu", "06 - Hóa đơn/Chứng từ" },
                { "07 - Ho so", "07 - Hồ sơ" }, { "Chua xac dinh", "Chưa xác định" }
            };
            string vietnamese;
            if(!names.TryGetValue(group, out vietnamese)) return group;
            return UiLanguage.English ? group : vietnamese;
        }
        void ResetSmartResults()
        {
            if (smartList == null || smartList.IsDisposed) return;
            smartResult = null;
            smartList.Items.Clear();
            btnSmartApply.Enabled = false;
            if(btnSmartSelect != null) btnSmartSelect.Enabled = false;
            UpdateSmartUndoCount();
            UpdateSmartSummary();
        }
        void UpdateSmartUndoCount()
        {
            if(btnSmartUndo == null || txtSmartFolder == null) return;
            try {
                string root = txtSmartFolder.Text;
                btnSmartUndo.Enabled = Directory.Exists(root) && SmartFolders.PendingUndo(root) > 0;
            } catch { btnSmartUndo.Enabled = false; }
        }
        void UpdateSmartSummary()
        {
            if(smartList == null || lblSmartCount == null || smartList.IsDisposed) return;
            int checkedCount = smartList.CheckedItems.Count;
            long bytes = smartList.CheckedItems.Cast<ListViewItem>().Sum(x => ((SmartPlan)x.Tag).Size);
            btnSmartApply.Enabled = checkedCount > 0 && !isBusy;
            if(btnSmartSelect != null) {
                btnSmartSelect.Enabled = smartResult != null && !isBusy;
                btnSmartSelect.Text = smartList.Items.Cast<ListViewItem>()
                    .Any(i => ((SmartPlan)i.Tag).Suggested && !i.Checked) ?
                    (UiLanguage.English ? "Select suggested" : "Chọn đề xuất") :
                    (UiLanguage.English ? "Clear selection" : "Bỏ chọn hết");
            }
            if(smartResult == null) {
                lblSmartCount.Text = UiLanguage.English ?
                    "Select a folder and click Scan & preview." : "Chọn thư mục rồi nhấn Quét & đề xuất.";
                return;
            }
            lblSmartCount.Text = UiLanguage.English ?
                "Found " + smartResult.Items.Count + " files | Select " + checkedCount + " (" + Fmt.Size(bytes) +
                ") | Duplicates " + smartResult.Duplicates + " | Protected " + smartResult.Protected :
                "Tìm thấy " + smartResult.Items.Count + " file | Đã chọn " + checkedCount + " (" + Fmt.Size(bytes) +
                ") | Trùng " + smartResult.Duplicates + " | Được bảo vệ " + smartResult.Protected;
        }
        async Task ScanSmartAsync()
        {
            string folder = txtSmartFolder.Text;
            bool work = radioSmartWork.Checked, content = chkSmartContent.Checked,
                date = chkSmartDate.Checked, dup = chkSmartDup.Checked;
            SmartFolders.ValidateRoot(folder);
            bool wholeDrive = SmartFolders.IsDriveRoot(folder);
            if(wholeDrive && !Confirm(UiLanguage.English ?
                "You selected an ENTIRE DATA DRIVE (" + folder +
                "). The app will only scan and preview; it will NOT move files until you manually select and confirm them. System, Zalo and application folders are excluded. Continue scanning?" :
                "Bạn đang chọn TOÀN BỘ Ổ DỮ LIỆU (" + folder +
                "). Phần mềm chỉ quét và xem trước; KHÔNG chuyển file cho đến khi bạn tự chọn và xác nhận. Thư mục hệ thống, Zalo và ứng dụng được loại trừ. Tiếp tục quét?"))
                return;
            smartLastFolder = folder;
            ResetSmartResults();
            SetBusy(true);
            var result = await Task.Run(() => SmartFolders.Scan(folder,work,content,date,dup,status.Report));
            smartResult = result;
            smartLoading = true;
            smartList.BeginUpdate();
            try {
                foreach(var p in result.Items) {
                    var item = new ListViewItem(Path.GetFileName(p.Source));
                    item.SubItems.Add(SmartGroupName(p.Group));
                    item.SubItems.Add(Fmt.Size(p.Size));
                    item.SubItems.Add(p.Reason);
                    item.SubItems.Add(p.Suggested ? p.Target : (UiLanguage.English ? "Keep original" : "Giữ nguyên"));
                    item.Tag=p;
                    item.Checked=p.Suggested && !wholeDrive;
                    if(!p.Suggested) item.ForeColor=Color.DimGray;
                    smartList.Items.Add(item);
                }
            } finally { smartList.EndUpdate(); smartLoading = false; }
            SetBusy(false);
            UpdateSmartSummary();
            UpdateSmartUndoCount();
            if(result.Limited)
                Msg(UiLanguage.English ?
                    "The scan reached its 20,000-file safety limit. Results are partial. Choose a smaller folder to scan the remaining files." :
                    "Đã đạt giới hạn an toàn 20.000 file. Danh sách hiện tại chưa đầy đủ. Hãy chọn thư mục nhỏ hơn để quét phần còn lại.",
                    MessageBoxIcon.Warning);
            Status(UiLanguage.English ? "Scan finished. Review all selected file moves." :
                "Đã quét xong. Hãy xem kỹ từng file trước khi áp dụng.");
        }
        async Task ApplySmartAsync()
        {
            if(smartResult == null) return;
            string root = txtSmartFolder.Text;
            var selected = smartList.CheckedItems.Cast<ListViewItem>().Select(i => (SmartPlan)i.Tag).ToList();
            if(selected.Count==0) return;
            long bytes=selected.Sum(x=>x.Size);
            string msg = UiLanguage.English ?
                "Move " + selected.Count + " selected files (" + Fmt.Size(bytes) + ") into:\n" +
                Path.Combine(root,SmartFolders.SortedFolder) +
                "\n\nNo files will be deleted or overwritten. Continue?" :
                "Chuyển " + selected.Count + " file đã chọn (" + Fmt.Size(bytes) + ") vào:\n" +
                Path.Combine(root,SmartFolders.SortedFolder) +
                "\n\nKHÔNG xóa hay ghi đè file. Bạn đã xem trước và đồng ý?";
            if(!Confirm(msg)) return;
            SetBusy(true);
            var done = await Task.Run(() => SmartFolders.Apply(root,selected,status.Report));
            ResetSmartResults();
            Status((UiLanguage.English ? "Moved " : "Đã chuyển ") + done.Success +
                (UiLanguage.English ? " files." : " file."));
            string report = (UiLanguage.English ? "Moved: " : "Đã chuyển: ") + done.Success +
                (UiLanguage.English ? "\nSkipped: " : "\nBỏ qua: ") + done.Skipped;
            if(done.Errors.Count>0)
                report += "\n" + string.Join("\n",done.Errors.Take(5));
            Msg(report,done.Errors.Count>0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            UpdateSmartUndoCount();
        }
        async Task UndoSmartAsync()
        {
            string root=txtSmartFolder.Text;
            SmartFolders.ValidateRoot(root);
            int count=SmartFolders.PendingUndo(root);
            if(count<=0) { Msg(UiLanguage.English ? "No changes to undo." : "Không có file nào cần hoàn tác."); return; }
            if(!Confirm(UiLanguage.English ?
                "Restore up to " + count + " moved files to their original locations?\nFiles modified since moving will be skipped." :
                "Khôi phục tối đa " + count + " file về vị trí cũ?\nFile đã thay đổi hoặc trùng đường dẫn sẽ được bỏ qua.")) return;
            SetBusy(true);
            var result=await Task.Run(() => SmartFolders.Undo(root,status.Report));
            ResetSmartResults();
            UpdateSmartUndoCount();
            Msg((UiLanguage.English ? "Restored: " : "Đã hoàn tác: ") + result.Success +
                (UiLanguage.English ? "\nSkipped: " : "\nBỏ qua: ") + result.Skipped +
                (result.Errors.Count>0 ? "\n" + string.Join("\n",result.Errors.Take(5)) : ""),
                result.Errors.Count>0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
