// Dọn Dẹp Máy Tính — tab Bản đồ ổ đĩa, File trùng, Đã chuyển đi, Tự động.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace DonDep
{
    partial class MainForm
    {
        // ================================================================= TAB 2: BẢN ĐỒ Ổ ĐĨA
        ComboBox cboMapDrive;
        ListView mapList;
        Label lblMapPath, lblInfoLevel, lblInfoTitle, lblInfoText;
        Button btnInfoRecycle, btnInfoDelete, btnInfoMove;
        ContextMenuStrip mapMenu;
        ToolStripItem miMapWhat, miMapRecycle, miMapDelete, miMapMove;
        MapNode mapRoot, mapCurrent;

        void BuildMapTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 82 };
            top.Controls.Add(Lbl("Ổ đĩa:", 8, 14, 50, 22));
            cboMapDrive = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(60, 10), Size = new Size(80, 24) };
            foreach (var d in Fs.FixedDrives()) cboMapDrive.Items.Add(d.Name);
            if (cboMapDrive.Items.Count > 0) cboMapDrive.SelectedIndex = 0;
            var btnScan = Btn("Phân tích ổ đĩa", 150, 6, 160, 32);
            btnScan.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            var btnUp = Btn("⬆ Lên trên", 318, 6, 110, 32);
            var btnOpen = Btn("Mở trong Explorer", 434, 6, 150, 32, false);
            var btnAdd = Btn("Thêm vào danh sách chuyển đi", 590, 6, 250, 32);
            top.Controls.Add(Lbl("Bấm vào một mục để biết đó là gì. Chuột phải để xóa hoặc chuyển đi.", 848, 4, 320, 42));
            lblMapPath = Lbl("Chọn ổ rồi bấm \"Phân tích ổ đĩa\" để xem thư mục nào chiếm nhiều chỗ nhất.", 8, 50, 1150, 24);
            lblMapPath.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { cboMapDrive, btnScan, btnUp, btnOpen, btnAdd, lblMapPath });

            mapList = new ListView { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill, HideSelection = false, MultiSelect = true, Font = new Font("Segoe UI", 10f) };
            mapList.Columns.Add("Tên", 330);
            mapList.Columns.Add("Dung lượng", 95, HorizontalAlignment.Right);
            mapList.Columns.Add("% ổ đĩa", 170);
            mapList.Columns.Add("Số file", 80, HorizontalAlignment.Right);
            mapList.Columns.Add("Đây là gì", 250);

            var info = new Panel { Dock = DockStyle.Right, Width = 300, Padding = new Padding(8) };
            lblInfoLevel = new Label { Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Text = "Chọn một mục" };
            lblInfoTitle = new Label { Dock = DockStyle.Top, Height = 26, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            lblInfoText = new Label { Dock = DockStyle.Fill, Text = "Bấm vào một thư mục hoặc file trong danh sách, app sẽ cho biết đó là gì và có xóa được không." };
            var btns = new Panel { Dock = DockStyle.Bottom, Height = 122 };
            btnInfoRecycle = Btn("Xóa (vào Thùng rác)", 0, 0, 284, 36);
            btnInfoDelete = Btn("Xóa vĩnh viễn", 0, 40, 284, 36);
            btnInfoMove = Btn("Chuyển lên Drive / ổ ngoài", 0, 80, 284, 36);
            btnInfoRecycle.Enabled = btnInfoDelete.Enabled = btnInfoMove.Enabled = false;
            btns.Controls.AddRange(new Control[] { btnInfoRecycle, btnInfoDelete, btnInfoMove });
            info.Controls.Add(lblInfoText); info.Controls.Add(btns); info.Controls.Add(lblInfoTitle); info.Controls.Add(lblInfoLevel);

            mapMenu = new ContextMenuStrip();
            miMapWhat = mapMenu.Items.Add("Đây là gì?");
            mapMenu.Items.Add(new ToolStripSeparator());
            miMapRecycle = mapMenu.Items.Add("Xóa (vào Thùng rác)");
            miMapDelete = mapMenu.Items.Add("Xóa vĩnh viễn");
            mapMenu.Items.Add(new ToolStripSeparator());
            miMapMove = mapMenu.Items.Add("Chuyển lên Drive / ổ ngoài");
            var miMapOpen = mapMenu.Items.Add("Mở trong Explorer");
            mapList.ContextMenuStrip = mapMenu;

            tab.Controls.Add(mapList);
            tab.Controls.Add(info);
            tab.Controls.Add(top);

            btnScan.Click += async (s, e) => await Guard(MapScanAsync);
            mapList.DoubleClick += (s, e) =>
            {
                if (mapList.SelectedItems.Count == 0) return;
                var n = (MapNode)mapList.SelectedItems[0].Tag;
                if (n.IsDir) ShowMapDir(n); else Fs.OpenInExplorer(n.Path);
            };
            btnUp.Click += (s, e) => { if (mapCurrent != null && mapCurrent.Parent != null) ShowMapDir(mapCurrent.Parent); };
            btnOpen.Click += (s, e) =>
            {
                if (mapList.SelectedItems.Count > 0) Fs.OpenInExplorer(((MapNode)mapList.SelectedItems[0].Tag).Path);
                else if (mapCurrent != null) Fs.OpenInExplorer(mapCurrent.Path);
            };
            btnAdd.Click += async (s, e) => await Guard(AddMapToMoveList);
            mapList.SelectedIndexChanged += (s, e) => ShowMapInfo();
            mapList.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Delete) await Guard(() => RemoveMapItems(e.Shift)); };
            btnInfoRecycle.Click += async (s, e) => await Guard(() => RemoveMapItems(false));
            btnInfoDelete.Click += async (s, e) => await Guard(() => RemoveMapItems(true));
            btnInfoMove.Click += async (s, e) => await Guard(AddMapToMoveList);
            mapMenu.Opening += (s, e) =>
            {
                var sel = SelectedMap();
                if (sel.Count == 0) { e.Cancel = true; return; }
                bool allCam = sel.All(n => n.Info.Level == Level.Cam);
                miMapRecycle.Enabled = miMapDelete.Enabled = !allCam;
                miMapMove.Enabled = sel.Any(n => !PathRules.IsProtected(n.Path));
                miMapWhat.Text = UiLanguage.T("Đây là gì?  —  " + sel[0].Info.Short);
            };
            miMapWhat.Click += (s, e) =>
            {
                var sel = SelectedMap();
                if (sel.Count == 0) return;
                var n = sel[0];
                Msg(n.Info.Label + "\n\n" + n.Info.Title + "\n\n" + n.Info.Text + "\n\n" + n.Path + "\n" + Fmt.Size(n.Size));
            };
            miMapRecycle.Click += async (s, e) => await Guard(() => RemoveMapItems(false));
            miMapDelete.Click += async (s, e) => await Guard(() => RemoveMapItems(true));
            miMapMove.Click += async (s, e) => await Guard(AddMapToMoveList);
            miMapOpen.Click += (s, e) => { var sel = SelectedMap(); if (sel.Count > 0) Fs.OpenInExplorer(sel[0].Path); };
        }

        List<MapNode> SelectedMap() { return mapList.SelectedItems.Cast<ListViewItem>().Select(i => (MapNode)i.Tag).ToList(); }

        async Task MapScanAsync()
        {
            string root = cboMapDrive.SelectedItem as string;
            if (root == null) return;
            SetBusy(true);
            App.Log("Bắt đầu phân tích ổ " + root + "...");
            mapRoot = await Task.Run(() => DiskMap.Scan(root, status.Report, CancellationToken.None));
            ShowMapDir(mapRoot);
            Status("Bấm đúp vào thư mục để xem bên trong. Bấm vào một mục để biết đó là gì; chuột phải để xóa hoặc chuyển đi.");
        }

        void ShowMapDir(MapNode dir)
        {
            mapCurrent = dir;
            long driveUsed = 0;
            try { var di = new DriveInfo(mapRoot.Path); driveUsed = di.TotalSize - di.AvailableFreeSpace; } catch { }
            double denom = driveUsed > 0 ? driveUsed : Math.Max(1, dir.Size);
            mapList.BeginUpdate();
            mapList.Items.Clear();
            foreach (var n in dir.Kids.Concat(dir.BigFiles).OrderByDescending(x => x.Size))
            {
                double pct = 100.0 * n.Size / denom;
                int bar = (int)Math.Round(Math.Min(20, pct / 5));
                string name = (n.IsDir ? "📁 " : "📄 ") + n.Name + (n.IsDir && n.Denied ? "  (một phần không đọc được)" : "");
                var it = new ListViewItem(name);
                it.SubItems.Add(Fmt.Size(n.Size));
                it.SubItems.Add(pct.ToString("0.0").PadLeft(5) + "% " + new string('█', bar));
                it.SubItems.Add(n.Count.ToString("N0"));
                it.SubItems.Add(n.Info.Short + " — " + n.Info.Title);
                it.UseItemStyleForSubItems = false;
                it.SubItems[4].ForeColor = n.Info.Color;
                if (n.Info.Level == Level.Cam) it.ForeColor = Color.Gray;
                it.Tag = n;
                mapList.Items.Add(it);
            }
            mapList.EndUpdate();
            UiLanguage.TranslateRows(mapList, 4);
            string note = "";
            if (dir == mapRoot && driveUsed - dir.Size > Fmt.GB)
                note = "   •   " + Fmt.Size(driveUsed - dir.Size) + " thuộc file hệ thống không đọc được" + (App.IsAdmin ? "" : " (thử Quyền admin)");
            lblMapPath.Text = dir.Path + "   —   " + Fmt.Size(dir.Size) + note;
            ShowMapInfo();
        }

        void ShowMapInfo()
        {
            var sel = SelectedMap();
            if (sel.Count == 0)
            {
                btnInfoRecycle.Enabled = btnInfoDelete.Enabled = btnInfoMove.Enabled = false;
                return;
            }
            var n = sel[0];
            lblInfoLevel.Text = n.Info.Label;
            lblInfoLevel.ForeColor = n.Info.Color;
            lblInfoTitle.Text = n.Info.Title;
            var sb = new StringBuilder(n.Info.Text);
            sb.Append("\n\n").Append(n.IsDir ? "Thư mục: " : "File: ").Append(UiLanguage.P(n.Name)).Append("\nDung lượng: ").Append(Fmt.Size(n.Size)).Append("\n").Append(UiLanguage.P(n.Path));
            if (sel.Count > 1) sb.Append("\n\n(Đang chọn ").Append(sel.Count).Append(" mục, tổng ").Append(Fmt.Size(sel.Sum(x => x.Size))).Append(")");
            lblInfoText.Text = sb.ToString();
            btnInfoRecycle.Enabled = btnInfoDelete.Enabled = n.Info.Level != Level.Cam;
            btnInfoMove.Enabled = !PathRules.IsProtected(n.Path);
        }

        async Task RemoveMapItems(bool permanent)
        {
            var sel = SelectedMap();
            if (sel.Count == 0) { Msg("Hãy chọn thư mục / file muốn xóa trước."); return; }
            var blocked = sel.Where(n => n.Info.Level == Level.Cam).ToList();
            if (blocked.Count > 0)
            {
                var t = new StringBuilder("Những mục sau KHÔNG ĐƯỢC XÓA:\n");
                foreach (var b in blocked) t.Append("\n⛔ ").Append(UiLanguage.P(b.Name)).Append(" — ").Append(b.Info.Title).Append("\n    ").Append(b.Info.Text).Append("\n");
                Msg(t.ToString(), MessageBoxIcon.Stop);
                sel = sel.Where(n => n.Info.Level != Level.Cam).ToList();
                if (sel.Count == 0) return;
            }
            var prot = sel.Where(n => Protect.Blocks(n.Path)).ToList();
            if (prot.Count > 0)
            {
                Msg(UiLanguage.P(UiLanguage.L("Các mục sau nằm trong danh sách bảo vệ (hoặc chứa mục được bảo vệ) nên sẽ không bị xóa:\n\n", "These items are protected (or contain a protected item) and will not be deleted:\n\n") +
                    string.Join("\n", prot.Select(n => "🛡 " + n.Path))), MessageBoxIcon.Information);
                sel = sel.Except(prot).ToList();
                if (sel.Count == 0) return;
            }
            long sum = sel.Sum(n => n.Size);
            var risky = sel.Where(n => n.Info.Level == Level.CanThan || n.Info.Level == Level.KhongRo).ToList();
            var msg = new StringBuilder(permanent ? "XÓA VĨNH VIỄN — không khôi phục được:\n" : "Chuyển vào Thùng rác (còn khôi phục được):\n");
            foreach (var n in sel.Take(10)) msg.Append("\n• ").Append(UiLanguage.P(n.Name)).Append("  (").Append(Fmt.Size(n.Size)).Append(")  —  ").Append(n.Info.Short).Append(": ").Append(n.Info.Title);
            if (sel.Count > 10) msg.Append("\n   ... và ").Append(sel.Count - 10).Append(" mục khác");
            msg.Append("\n\nTổng: ").Append(Fmt.Size(sum));
            if (risky.Count > 0) { msg.Append("\n\n⚠️ CHÚ Ý:"); foreach (var n in risky) msg.Append("\n").Append(UiLanguage.P(n.Name)).Append(": ").Append(n.Info.Text); }
            if (!permanent) msg.Append("\n\nLưu ý: ổ đĩa chỉ trống thêm sau khi dọn Thùng rác. File quá lớn có thể bị Windows hỏi xóa hẳn.");
            msg.Append("\n\nTiếp tục?");
            if (!Confirm(msg.ToString())) return;
            if (risky.Count > 0 || (permanent && sum > Fmt.GB))
            {
                string again = "Bạn chắc chắn chứ?\n\n" + (risky.Count > 0 ? "Có " + risky.Count + " mục cần cẩn thận / chưa rõ là gì. " : "") +
                               (permanent ? "Xóa vĩnh viễn " + Fmt.Size(sum) + " — không lấy lại được." : "Các mục sẽ vào Thùng rác.");
                if (!Confirm(again)) return;
            }
            SetBusy(true);
            var freeBefore = Fs.FreeMap();
            int ok = 0, partial = 0;
            var remainingMap = new Dictionary<MapNode, long>();
            await RunSta(() =>
            {
                foreach (var n in sel)
                {
                    status.Report("Đang xóa: " + n.Path);
                    try
                    {
                        if (n.IsDir)
                        {
                            if (permanent) Fs.RemoveTreeSafe(n.Path, DateTime.MaxValue);
                            else FileSystem.DeleteDirectory(n.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            if (permanent) { File.SetAttributes(n.Path, FileAttributes.Normal); File.Delete(n.Path); }
                            else FileSystem.DeleteFile(n.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                    }
                    catch (Exception ex) { App.Log("Không xóa hết được " + n.Path + ": " + ex.Message); }
                    long remaining = 0;
                    if (n.IsDir && Directory.Exists(n.Path)) remaining = Fs.FolderSize(n.Path);
                    else if (!n.IsDir && File.Exists(n.Path)) { try { remaining = new FileInfo(n.Path).Length; } catch { } }
                    if (remaining <= 0) ok++; else if (remaining < n.Size) partial++;
                    remainingMap[n] = remaining;
                    App.Log("Đã xóa " + (permanent ? "vĩnh viễn" : "vào Thùng rác") + ": " + n.Path + " (" + Fmt.Size(n.Size - remaining) + ")");
                }
            });
            foreach (var kv in remainingMap) DiskMap.AfterDelete(kv.Key, kv.Value);
            ShowMapDir(mapCurrent);
            UpdateDrives();
            var freeAfter = Fs.FreeMap();
            string report = "";
            foreach (var k in freeAfter.Keys.OrderBy(x => x)) { long b; if (freeBefore.TryGetValue(k, out b) && freeAfter[k] - b > 10 * Fmt.MB) report += "\n    " + k + "  +" + Fmt.Size(freeAfter[k] - b) + "  (nay còn trống " + Fmt.Size(freeAfter[k]) + ")"; }
            if (permanent) { AppState.AddFreed(AppState.Gain(freeBefore, freeAfter)); UpdateStatsLabel(); }
            var gone = remainingMap.Where(kv => kv.Value < kv.Key.Size).ToList();
            History.Add(permanent ? "xoa" : "thungrac", gone.Count, gone.Sum(kv => kv.Key.Size - kv.Value), gone.Select(kv => kv.Key.Path));
            string done = "Đã xóa xong " + ok + "/" + sel.Count + " mục.";
            if (partial > 0 || ok < sel.Count) done += "\n\nMột số file đang được dùng nên chưa xóa được. Hãy tắt phần mềm đang dùng chúng rồi thử lại.";
            if (report.Length > 0) done += "\n\nĐã giải phóng:" + report;
            else if (!permanent) done += "\n\nCác mục đang nằm trong Thùng rác. Dọn Thùng rác (tab Dọn dẹp) để thật sự lấy lại dung lượng.";
            Status("Đã xóa xong.");
            Msg(done);
        }

        async Task AddMapToMoveList()
        {
            var sel = SelectedMap();
            if (sel.Count == 0) { Msg("Hãy chọn một hoặc nhiều thư mục / file trong danh sách trước."); return; }
            var blocked = sel.Where(n => PathRules.IsProtected(n.Path)).ToList();
            if (blocked.Count > 0)
            {
                Msg("Không thể chuyển những mục này vì là thư mục hệ thống, chương trình hoặc dữ liệu tin nhắn (chuyển đi sẽ làm hỏng Windows hoặc phần mềm):\n\n" + string.Join("\n", blocked.Select(b => b.Path)), MessageBoxIcon.Warning);
                sel = sel.Where(n => !PathRules.IsProtected(n.Path)).ToList();
                if (sel.Count == 0) return;
            }
            SetBusy(true);
            var seen = new HashSet<string>(list.Items.Cast<ListViewItem>().Select(i => ((CleanItem)i.Tag).Path), StringComparer.OrdinalIgnoreCase);
            var added = await Task.Run(() =>
            {
                var r = new List<CleanItem>();
                foreach (var s in sel)
                {
                    string root = Path.GetDirectoryName(s.Path);
                    var files = s.IsDir ? Fs.Walk(s.Path, null, 0, null, null, status.Report) : new List<FileInfo> { new FileInfo(s.Path) };
                    foreach (var f in files)
                    {
                        if (PathRules.IsProtected(f.FullName) || !seen.Add(f.FullName)) continue;
                        r.Add(new CleanItem { Category = "tu", Kind = "chuyen", Path = f.FullName, Root = root, Size = f.Length, Name = f.Name, Folder = f.DirectoryName, Date = f.LastWriteTime, Action = "chuyen" });
                    }
                }
                return r;
            });
            SetBusy(false);
            if (added.Count == 0) { Msg("Không có file nào mới để thêm."); return; }
            long sum = added.Sum(o => o.Size);
            if (added.Count > 3000 && !Confirm("Sẽ thêm " + added.Count + " file (" + Fmt.Size(sum) + ") vào danh sách. Tiếp tục?")) return;
            suspendSummary = true;
            list.BeginUpdate();
            list.Items.AddRange(added.Select(o => NewRow(o, true)).ToArray());
            UpdateGroupHeaders();
            list.EndUpdate();
            suspendSummary = false;
            UpdateSummary();
            App.Log("Đã thêm " + added.Count + " file (" + Fmt.Size(sum) + ") vào nhóm Tự chọn.");
            tabs.SelectedIndex = 0;
            Status("Đã thêm vào nhóm \"Tự chọn\". Chọn nơi lưu rồi bấm \"Dọn dẹp ngay\" để chuyển đi.");
        }

        // ================================================================= TAB 3: FILE TRÙNG
        ListBox lstDupFolders;
        NumericUpDown numDupMin;
        ListView dupList;
        Label lblDupSum;
        PictureBox dupPic; Label dupLbl;

        void BuildDupTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 140 };
            top.Controls.Add(Lbl("Tìm file giống hệt nhau trong các thư mục:", 8, 8, 300, 22));
            lstDupFolders = new ListBox { Location = new Point(8, 32), Size = new Size(520, 98) };
            var folders = cfg.DupFolders.Count > 0 ? cfg.DupFolders : DefaultDupFolders();
            foreach (var f in folders) if (Directory.Exists(f)) lstDupFolders.Items.Add(f);
            var btnAdd = Btn("Thêm thư mục…", 536, 32, 130, 30);
            var btnRemove = Btn("Bỏ thư mục", 536, 66, 130, 30);
            numDupMin = Num(top, "Chỉ xét file từ (MB):", 32, 0, 10000, cfg.DupMinMB, 690, 150);
            var btnScan = Btn("Tìm file trùng", 690, 66, 230, 40);
            btnScan.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            var btnDel = Btn("Xóa bản trùng đã chọn", 928, 66, 230, 40);
            btnDel.BackColor = Yellow;
            lblDupSum = Lbl("", 690, 110, 470, 24);
            top.Controls.AddRange(new Control[] { lstDupFolders, btnAdd, btnRemove, btnScan, btnDel, lblDupSum });

            dupList = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Dock = DockStyle.Fill, ShowGroups = true, HideSelection = false };
            dupList.Columns.Add("Tên", 300);
            dupList.Columns.Add("Dung lượng", 90, HorizontalAlignment.Right);
            dupList.Columns.Add("Ngày sửa", 90);
            dupList.Columns.Add("Thư mục", 340);
            dupList.Columns.Add("Gợi ý", 80);
            var prev = NewPreviewPanel(out dupPic, out dupLbl);
            tab.Controls.Add(dupList);
            tab.Controls.Add(prev);
            tab.Controls.Add(top);

            btnAdd.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Chọn thư mục để tìm file trùng" })
                    if (dlg.ShowDialog(this) == DialogResult.OK && !lstDupFolders.Items.Contains(dlg.SelectedPath)) lstDupFolders.Items.Add(dlg.SelectedPath);
            };
            btnRemove.Click += (s, e) => { if (lstDupFolders.SelectedIndex >= 0) lstDupFolders.Items.RemoveAt(lstDupFolders.SelectedIndex); };
            btnScan.Click += async (s, e) => await Guard(DupScanAsync);
            btnDel.Click += async (s, e) => await Guard(RemoveDups);
            dupList.SelectedIndexChanged += (s, e) => { if (dupList.SelectedItems.Count > 0) ShowPreview(dupPic, dupLbl, (string)dupList.SelectedItems[0].Tag); };
            dupList.DoubleClick += (s, e) => { if (dupList.SelectedItems.Count > 0) Fs.OpenInExplorer((string)dupList.SelectedItems[0].Tag); };
        }

        static List<string> DefaultDupFolders()
        {
            var l = new List<string> { Fs.Downloads };
            foreach (var f in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos })
                l.Add(Environment.GetFolderPath(f));
            foreach (var d in Fs.FixedDrives()) if (!d.Name.TrimEnd('\\').Equals(Fs.SystemDrive, StringComparison.OrdinalIgnoreCase)) l.Add(d.RootDirectory.FullName);
            return l.Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        async Task DupScanAsync()
        {
            var folders = lstDupFolders.Items.Cast<string>().ToList();
            if (folders.Count == 0) { Msg("Hãy thêm ít nhất một thư mục để tìm file trùng."); return; }
            cfg.DupFolders = folders; cfg.DupMinMB = (int)numDupMin.Value; cfg.Save();
            SetBusy(true);
            long min = (long)numDupMin.Value * Fmt.MB;
            var found = await Task.Run(() => DupFinder.Find(folders, min, status.Report, CancellationToken.None));
            dupList.BeginUpdate();
            dupList.Items.Clear(); dupList.Groups.Clear();
            long waste = 0; int gi = 0;
            foreach (var grp in found)
            {
                gi++;
                var lg = new ListViewGroup("g" + gi, "Nhóm " + gi + ": " + grp.Files.Count + " bản giống hệt nhau, mỗi bản " + Fmt.Size(grp.Files[0].Length));
                dupList.Groups.Add(lg);
                bool first = true;
                foreach (var f in grp.Files)
                {
                    var it = new ListViewItem(f.Name);
                    it.SubItems.Add(Fmt.Size(f.Length));
                    it.SubItems.Add(f.LastWriteTime.ToString("dd/MM/yyyy"));
                    it.SubItems.Add(f.DirectoryName);
                    it.SubItems.Add(first ? "giữ lại" : "bản trùng");
                    if (!first) waste += f.Length;
                    it.Checked = !first;
                    it.Tag = f.FullName;
                    it.Group = lg;
                    dupList.Items.Add(it);
                    first = false;
                }
            }
            dupList.EndUpdate();
            UiLanguage.TranslateRows(dupList, 4);
            lblDupSum.Text = "Tìm thấy " + found.Count + " nhóm file trùng — bản thừa chiếm " + Fmt.Size(waste) + ". Mỗi nhóm đã chọn sẵn để giữ lại 1 bản.";
            App.Log("File trùng: " + found.Count + " nhóm, thừa " + Fmt.Size(waste));
            Status("Xem lại các bản được chọn rồi bấm \"Xóa bản trùng đã chọn\".");
        }

        async Task RemoveDups()
        {
            var items = dupList.Items.Cast<ListViewItem>().ToList();
            var chk = items.Where(i => i.Checked).ToList();
            if (chk.Count == 0) { Msg("Chưa chọn bản trùng nào."); return; }
            var bad = items.GroupBy(i => i.Group).Where(g => g.All(i => i.Checked)).Select(g => g.Key.Header).ToList();
            if (bad.Count > 0) { Msg("Các nhóm sau đang bị chọn hết tất cả các bản:\n\n" + string.Join("\n", bad) + "\n\nHãy bỏ chọn ít nhất 1 bản trong mỗi nhóm để giữ lại.", MessageBoxIcon.Warning); return; }
            long sum = chk.Sum(i => new FileInfo((string)i.Tag).Exists ? new FileInfo((string)i.Tag).Length : 0);
            if (!Confirm("Chuyển " + chk.Count + " bản trùng (" + Fmt.Size(sum) + ") vào Thùng rác?\n\nMỗi nhóm vẫn còn giữ lại ít nhất 1 bản. Dung lượng chỉ được giải phóng hẳn sau khi dọn Thùng rác (tab Dọn dẹp làm được việc này).")) return;
            SetBusy(true);
            var removed = new List<ListViewItem>();
            var paths = chk.Select(i => (string)i.Tag).Where(p => !Protect.Has(p)).ToList();
            var okPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await RunSta(() =>
            {
                foreach (var p in paths)
                {
                    try { FileSystem.DeleteFile(p, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); okPaths.Add(p); }
                    catch (Exception ex) { App.Log("Không xóa được " + p + ": " + ex.Message); }
                }
            });
            foreach (var it in chk) if (okPaths.Contains((string)it.Tag)) dupList.Items.Remove(it);
            App.Log("Đã chuyển " + okPaths.Count + " bản trùng vào Thùng rác.");
            History.Add("trung", okPaths.Count, 0, okPaths);
            Msg("Đã chuyển " + okPaths.Count + " bản trùng vào Thùng rác.");
        }

        // ================================================================= TAB 4: ĐÃ CHUYỂN ĐI
        ListView movedList;
        Label lblMovedSum;

        void BuildMovedTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 56 };
            var btnReload = Btn("Tải lại danh sách", 8, 10, 150, 34);
            var btnRestore = Btn("Lấy về chỗ cũ", 166, 10, 170, 34);
            btnRestore.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnRestore.BackColor = Yellow;
            lblMovedSum = Lbl("", 350, 18, 800, 24);
            top.Controls.AddRange(new Control[] { btnReload, btnRestore, lblMovedSum });
            movedList = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, Dock = DockStyle.Fill };
            movedList.Columns.Add("Thời gian", 130);
            movedList.Columns.Add("Loại", 90);
            movedList.Columns.Add("Tên file", 250);
            movedList.Columns.Add("Dung lượng", 90, HorizontalAlignment.Right);
            movedList.Columns.Add("Trạng thái", 120);
            movedList.Columns.Add("Chỗ cũ trên máy", 280);
            movedList.Columns.Add("Nơi lưu", 300);
            tab.Controls.Add(movedList);
            tab.Controls.Add(top);
            btnReload.Click += (s, e) => LoadMovedList();
            btnRestore.Click += async (s, e) => await Guard(RestoreMoved);
        }

        void LoadMovedList()
        {
            var all = MovedStore.LoadAll();
            movedList.BeginUpdate();
            movedList.Items.Clear();
            foreach (var e in all)
            {
                var it = new ListViewItem(e.Time);
                it.SubItems.Add(e.Category);
                it.SubItems.Add(Path.GetFileName(e.Orig));
                it.SubItems.Add(e.SizeText);
                it.SubItems.Add(e.State);
                it.SubItems.Add(e.Orig);
                it.SubItems.Add(e.Dest);
                it.Tag = e;
                if (e.State != "Đang ở nơi lưu") it.ForeColor = Color.Gray;
                movedList.Items.Add(it);
            }
            movedList.EndUpdate();
            UiLanguage.TranslateRows(movedList, 1, 4);
            lblMovedSum.Text = "Tổng cộng " + all.Count + " file đã từng chuyển đi. Đánh dấu file muốn lấy về rồi bấm \"Lấy về chỗ cũ\".";
        }

        async Task RestoreMoved()
        {
            var sel = movedList.CheckedItems.Cast<ListViewItem>().Select(i => (MovedEntry)i.Tag).Where(e => e.State == "Đang ở nơi lưu").ToList();
            if (sel.Count == 0) { Msg("Hãy đánh dấu những file (đang ở nơi lưu) muốn lấy về."); return; }
            if (sel.Any(e => e.Code.StartsWith(App.RemoteName + ":")) && !Rclone.Ready) { Msg("Một số file nằm trên Google Drive. Hãy bấm \"Kết nối Google Drive\" ở tab Dọn dẹp trước.", MessageBoxIcon.Warning); return; }
            var need = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in sel) { string r = Fs.Root(e.Orig); long v; need.TryGetValue(r, out v); need[r] = v + e.Bytes; }
            string msg = "Lấy " + sel.Count + " file về đúng chỗ cũ trên máy?\n";
            foreach (var kv in need)
            {
                long free = Fs.DriveFree(kv.Key), after = free - kv.Value;
                if (after < Fmt.GB) { Msg("Ổ " + kv.Key + " không đủ chỗ để lấy về (còn " + Fmt.Size(free) + ", cần " + Fmt.Size(kv.Value) + ", và nên chừa ít nhất 1 GB). Hãy dọn bớt trước.", MessageBoxIcon.Warning); return; }
                msg += "\n    " + kv.Key + "  còn " + Fmt.Size(free) + "  →  khoảng " + Fmt.Size(after);
            }
            msg += "\n\nBản lưu trên Drive / ổ ngoài vẫn được giữ nguyên.";
            if (!Confirm(msg)) return;
            SetBusy(true);
            int ok = await Task.Run(() =>
            {
                int n = 0;
                foreach (var e in sel)
                {
                    try
                    {
                        status.Report("Đang lấy về: " + Path.GetFileName(e.Orig));
                        Directory.CreateDirectory(Path.GetDirectoryName(e.Orig));
                        if (e.Code.StartsWith(App.RemoteName + ":"))
                        {
                            var r = Rclone.CopyTo(e.Code, e.Orig);
                            if (r.Code != 0) throw new Exception("rclone: " + r.Err);
                        }
                        else
                        {
                            if (!File.Exists(e.Code)) throw new Exception("Không tìm thấy bản lưu (ổ cứng ngoài / USB đã được cắm chưa?).");
                            File.Copy(e.Code, e.Orig, true);
                        }
                        if (!File.Exists(e.Orig)) throw new Exception("File chưa về tới máy.");
                        MovedStore.MarkRestored(e);
                        n++;
                    }
                    catch (Exception ex) { App.Log("Không lấy về được " + e.Orig + ": " + ex.Message); }
                }
                return n;
            });
            App.Log("Đã lấy về " + ok + "/" + sel.Count + " file.");
            History.Add("laylai", ok, sel.Where(x => File.Exists(x.Orig)).Sum(x => x.Bytes), sel.Where(x => File.Exists(x.Orig)).Select(x => x.Orig));
            LoadMovedList();
            UpdateDrives();
            Msg("Đã lấy về " + ok + "/" + sel.Count + " file.");
        }

        // ================================================================= SETTINGS (ALWAYS LAST)
        CheckBox chkStartup, chkAutoClean, chkZaloAuto;
        NumericUpDown numAlert, numAutoDays, numZaloDays, numZaloEvery;
        Label lblStats, lblZaloLast;
        Button btnSettingsVietnamese, btnSettingsEnglish;

        static void StyleSettingsLanguage(Button button, bool selected)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = selected ? 2 : 1;
            button.FlatAppearance.BorderColor = selected ?
                Color.FromArgb(38, 105, 215) : Color.FromArgb(204, 216, 234);
            button.BackColor = selected ?
                Color.FromArgb(225, 238, 255) : Color.White;
            button.ForeColor = selected ?
                Color.FromArgb(26, 86, 173) : Color.FromArgb(51, 65, 85);
            button.Font = new Font("Segoe UI", 10, selected ? FontStyle.Bold : FontStyle.Regular);
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
        }

        void BuildAutoTab(TabPage tab)
        {
            // Scrollable settings page: fits smaller Windows resolutions without clipping.
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = Color.FromArgb(245, 248, 252) };
            var content = new Panel { Location = Point.Empty, Size = new Size(1088, 1330),
                BackColor = Color.FromArgb(245, 248, 252) };
            scroll.Controls.Add(content);
            tab.Controls.Add(scroll);
            settingsContent = content;

            var title = Lbl("Cài đặt & tùy chỉnh", 24, 15, 1000, 36);
            title.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(31, 56, 95);
            var subtitle = Lbl("Thiết lập ngôn ngữ, chạy nền, biểu tượng và cập nhật ứng dụng.", 25, 52, 1000, 24);
            subtitle.ForeColor = Color.FromArgb(100, 116, 139);
            content.Controls.AddRange(new Control[] { title, subtitle });

            var grpLanguage = new GroupBox { Text = "🌐 Ngôn ngữ giao diện",
                Location = new Point(16, 91), Size = new Size(1050, 122), BackColor = Color.White };
            grpLanguage.Controls.Add(Lbl("Chọn ngôn ngữ hiển thị. Thay đổi được lưu tự động và áp dụng ngay.", 20, 25, 970, 24));
            btnSettingsVietnamese = new Button { Text = "🇻🇳 Tiếng Việt",
                Location = new Point(20, 56), Size = new Size(183, 46) };
            btnSettingsEnglish = new Button { Text = "🇬🇧 English",
                Location = new Point(217, 56), Size = new Size(183, 46) };
            StyleSettingsLanguage(btnSettingsVietnamese, !UiLanguage.English);
            StyleSettingsLanguage(btnSettingsEnglish, UiLanguage.English);
            var lblLanguageStatus = Lbl(
                UiLanguage.English ? "Current language: English" : "Đang sử dụng: Tiếng Việt",
                426, 68, 390, 26);
            lblLanguageStatus.ForeColor = Color.FromArgb(76, 101, 134);
            grpLanguage.Controls.AddRange(new Control[] {
                btnSettingsVietnamese, btnSettingsEnglish, lblLanguageStatus });
            btnSettingsVietnamese.Click += (s, e) => SwitchLanguage("vi");
            btnSettingsEnglish.Click += (s, e) => SwitchLanguage("en");
            busyControls.Add(btnSettingsVietnamese);
            busyControls.Add(btnSettingsEnglish);
            content.Controls.Add(grpLanguage);

            // ---- Thành tích: tổng dung lượng đã giải phóng + ảnh khoe Facebook
            var grpStats = new GroupBox { Text = UiLanguage.L("🏆 Thành tích của bạn", "🏆 Your results"),
                Location = new Point(16, 226), Size = new Size(1050, 112), BackColor = Color.White };
            lblStats = Lbl("", 20, 28, 1010, 30);
            lblStats.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblStats.ForeColor = Color.FromArgb(22, 101, 52);
            var btnShare = Btn(UiLanguage.L("📸 Tạo ảnh khoe lên Facebook", "📸 Create a picture to share"), 20, 64, 260, 36, false);
            grpStats.Controls.Add(Lbl(UiLanguage.L("Ảnh được lưu trong thư mục Ảnh (Pictures) và đã chép sẵn — vào Facebook bấm Ctrl+V để dán.",
                                                   "The picture is saved in Pictures and copied — press Ctrl+V in Facebook to paste it."), 292, 72, 740, 22));
            grpStats.Controls.AddRange(new Control[] { lblStats, btnShare });
            content.Controls.Add(grpStats);
            UpdateStatsLabel();
            btnShare.Click += (s, e) =>
            {
                AppState.Load();
                if (AppState.TotalFreed <= 0) { Msg(UiLanguage.L("Chưa có số liệu. Hãy dọn máy một lần rồi quay lại đây nhé!", "No results yet. Clean your PC once, then come back!")); return; }
                string file = ShareCard.Create(AppState.TotalFreed, AppState.Cleanups);
                try { using (var img = Image.FromFile(file)) Clipboard.SetImage(img); } catch { }
                try { Process.Start(file); } catch { }
                App.Log("Đã tạo ảnh chia sẻ: " + file);
                Status(UiLanguage.L("Đã tạo ảnh và chép vào bộ nhớ tạm. Vào Facebook bấm Ctrl+V để dán.", "Picture created and copied. Press Ctrl+V in Facebook to paste it."));
            };

            var grp = new GroupBox { Text = "Tự động & chạy nền cùng Windows",
                Location = new Point(16, 351), Size = new Size(1050, 250), BackColor = Color.White };
            chkStartup = new CheckBox { Text = "Bật: tự chạy ngầm khi mở máy (biểu tượng nhỏ ở góc phải thanh Taskbar)", Location = new Point(16, 28), Size = new Size(690, 26), Checked = File.Exists(Shortcut.StartupLink) };
            numAlert = Num(grp, "Cảnh báo khi một ổ còn trống dưới (GB):", 66, 1, 500, cfg.AlertGB, 36, 300);
            chkAutoClean = new CheckBox { Text = "Tự động dọn RÁC HỆ THỐNG theo lịch (không bao giờ tự chuyển hay xóa file cá nhân)", Location = new Point(16, 104), Size = new Size(690, 26), Checked = cfg.AutoClean };
            numAutoDays = Num(grp, "Dọn mỗi (ngày):", 140, 1, 90, cfg.AutoDays, 36, 300);
            string last = "chưa lần nào";
            DateTime lt;
            if (DateTime.TryParse(cfg.LastAutoClean, out lt)) last = lt.ToString("dd/MM/yyyy HH:mm");
            var lblLast = Lbl("Lần tự dọn gần nhất: " + last, 36, 176, 600, 22);
            var btnTray = Btn("Lưu và bật chạy ngầm ngay", 36, 204, 260, 34, false);
            var btnSave = Btn("Lưu thiết lập", 306, 204, 160, 34, false);
            btnSave.BackColor = Color.FromArgb(228, 238, 255);
            btnSave.ForeColor = Color.FromArgb(31, 83, 160);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(165, 195, 240);
            grp.Controls.AddRange(new Control[] { chkStartup, chkAutoClean, lblLast, btnTray, btnSave });

            // ---- Tự đưa ảnh / video / file Zalo cũ lên Google Drive
            var grpZalo = new GroupBox { Text = UiLanguage.L("☁ Tự động đưa ảnh, video, file Zalo cũ lên Google Drive", "☁ Automatically move old Zalo photos, videos and files to Google Drive"),
                Location = new Point(16, 614), Size = new Size(1050, 196), BackColor = Color.White };
            chkZaloAuto = new CheckBox { Text = UiLanguage.L("Bật: app chạy ngầm sẽ tự chuyển file Zalo cũ lên Drive theo lịch (không bao giờ đụng tin nhắn)",
                                                             "On: the background app moves old Zalo files to Drive on a schedule (messages are never touched)"),
                Location = new Point(16, 28), Size = new Size(1010, 26), Checked = cfg.ZaloAuto };
            numZaloDays = Num(grpZalo, UiLanguage.L("Chuyển file cũ hơn (ngày):", "Move files older than (days):"), 62, 7, 3650, cfg.ZaloAutoDays, 36, 300);
            numZaloEvery = Num(grpZalo, UiLanguage.L("Chạy mỗi (ngày):", "Run every (days):"), 96, 1, 90, cfg.ZaloAutoEvery, 36, 300);
            lblZaloLast = Lbl("", 36, 128, 990, 22);
            var btnZaloNow = Btn(UiLanguage.L("Xem trước & chạy ngay", "Preview & run now"), 36, 152, 220, 34);
            grpZalo.Controls.Add(Lbl(UiLanguage.L("File chỉ bị bỏ khỏi máy sau khi đã tải lên Drive và kiểm tra xong. Lấy lại ở tab \"Đã chuyển đi\". Cần bật chạy ngầm và kết nối Google Drive.",
                                                  "Files are removed from the PC only after upload and verification. Restore from \"Moved files\". Requires background mode and Google Drive."), 270, 150, 760, 40));
            grpZalo.Controls.AddRange(new Control[] { chkZaloAuto, lblZaloLast, btnZaloNow });
            content.Controls.Add(grpZalo);
            UpdateZaloLastLabel();
            chkZaloAuto.CheckedChanged += (s, e) =>
            {
                if (chkZaloAuto.Checked && !Rclone.Ready)
                {
                    Msg(UiLanguage.L("Hãy kết nối Google Drive trước: tab Dọn dẹp → \"Kết nối Google Drive\".", "Connect Google Drive first: Cleanup tab → \"Connect Google Drive\"."), MessageBoxIcon.Warning);
                    chkZaloAuto.Checked = false;
                }
            };
            btnZaloNow.Click += async (s, e) => await Guard(RunZaloAutoNowAsync);

            // ---- Danh sách bảo vệ
            var grpProt = new GroupBox { Text = UiLanguage.L("🛡 Danh sách bảo vệ — không bao giờ dọn, xóa hay chuyển", "🛡 Protected list — never cleaned, deleted or moved"),
                Location = new Point(16, 823), Size = new Size(1050, 170), BackColor = Color.White };
            lstProtect = new ListBox { Location = new Point(16, 26), Size = new Size(800, 132), HorizontalScrollbar = true };
            var btnProtDir = Btn(UiLanguage.L("Thêm thư mục…", "Add folder…"), 830, 26, 200, 32, false);
            var btnProtFile = Btn(UiLanguage.L("Thêm file…", "Add file…"), 830, 64, 200, 32, false);
            var btnProtDel = Btn(UiLanguage.L("Bỏ khỏi danh sách", "Remove from list"), 830, 102, 200, 32, false);
            grpProt.Controls.AddRange(new Control[] { lstProtect, btnProtDir, btnProtFile, btnProtDel });
            content.Controls.Add(grpProt);
            RefreshProtectList();
            Action<string> addProt = p =>
            {
                if (string.IsNullOrEmpty(p)) return;
                if (!cfg.Protected.Contains(p, StringComparer.OrdinalIgnoreCase)) cfg.Protected.Add(p);
                cfg.Save(); RefreshProtectList();
            };
            btnProtDir.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = UiLanguage.L("Chọn thư mục không bao giờ được dọn", "Choose a folder that must never be cleaned") })
                    if (dlg.ShowDialog(this) == DialogResult.OK) addProt(dlg.SelectedPath);
            };
            btnProtFile.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Title = UiLanguage.L("Chọn file không bao giờ được dọn", "Choose a file that must never be cleaned"), CheckFileExists = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK) addProt(dlg.FileName);
            };
            btnProtDel.Click += (s, e) =>
            {
                if (lstProtect.SelectedItem == null) return;
                string p = lstProtect.SelectedItem.ToString();
                cfg.Protected.RemoveAll(x => x.Equals(p, StringComparison.OrdinalIgnoreCase));
                cfg.Save(); RefreshProtectList();
            };

            var grpShort = new GroupBox { Text = "Biểu tượng & truy cập nhanh", Location = new Point(16, 1006), Size = new Size(1050, 100), BackColor = Color.White };
            grpShort.Controls.Add(Lbl("Tạo biểu tượng \"Dọn Dẹp Máy Tính\" trên Desktop và trong menu Start để mở app nhanh.", 16, 28, 1010, 22));
            var btnShortcut = Btn("Tạo biểu tượng", 16, 54, 180, 34, false);
            grpShort.Controls.Add(btnShortcut);

            var grpUpd = new GroupBox { Text = "Phiên bản & cập nhật", Location = new Point(16, 1119), Size = new Size(1050, 100), BackColor = Color.White };
            grpUpd.Controls.Add(Lbl("Bạn đang dùng bản " + App.Version + ". App tự kiểm tra bản mới mỗi lần mở. Trang chủ: github.com/" + App.Repo, 16, 28, 1010, 22));
            var btnUpdate = Btn("Kiểm tra bản mới", 16, 54, 180, 34, false);
            var btnHome = Btn("Mở trang chủ", 206, 54, 160, 34, false);
            grpUpd.Controls.AddRange(new Control[] { btnUpdate, btnHome });
            content.Controls.AddRange(new Control[] { grp, grpShort, grpUpd });

            btnSave.Click += (s, e) => SaveAutoSettings(false);
            btnTray.Click += (s, e) =>
            {
                SaveAutoSettings(true);
                Shortcut.StartTray();
                App.Log("Đã bật chạy ngầm (biểu tượng ở góc phải thanh Taskbar).");
                Msg("Đã bật chạy ngầm. Biểu tượng của app nằm ở góc phải thanh Taskbar (có thể nằm trong mũi tên ^).");
            };
            btnShortcut.Click += (s, e) =>
            {
                var created = new List<string>();
                var failed = new List<string>();
                Action<Environment.SpecialFolder, string> createAt = (location, label) =>
                {
                    try
                    {
                        string directory = Environment.GetFolderPath(location);
                        if (string.IsNullOrWhiteSpace(directory))
                            throw new DirectoryNotFoundException("Windows could not locate the " + label + " folder.");
                        string name = "Dọn Dẹp Máy Tính.lnk";
                        Shortcut.Create(Path.Combine(directory, name), "", App.Name);
                        created.Add(label);
                        App.Log("Đã tạo shortcut tại: " + Path.Combine(directory, name));
                    }
                    catch (Exception ex)
                    {
                        App.Log("LỖI TẠO SHORTCUT (" + label + "): " + ex);
                        failed.Add(label + ": " + ex.GetBaseException().Message);
                    }
                };
                createAt(Environment.SpecialFolder.DesktopDirectory, "Desktop");
                createAt(Environment.SpecialFolder.Programs, "Start Menu");
                // Refresh the already-enabled startup shortcut only; do not change its setting.
                if (File.Exists(Shortcut.StartupLink))
                {
                    try { Shortcut.SetStartup(true); }
                    catch (Exception ex)
                    {
                        App.Log("LỖI CẬP NHẬT SHORTCUT STARTUP: " + ex);
                        failed.Add("Startup: " + ex.GetBaseException().Message);
                    }
                }
                if (UiLanguage.English)
                    Msg((created.Count > 0 ? "Shortcut created: " + string.Join(", ", created) + "." : "Could not create a shortcut.") +
                        (failed.Count > 0 ? "\n\nDetails:\n" + string.Join("\n", failed) : ""),
                        failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                else
                    Msg((created.Count > 0 ? "Đã tạo biểu tượng tại: " + string.Join(", ", created) + "." : "Chưa tạo được biểu tượng.") +
                        (failed.Count > 0 ? "\n\nChi tiết lỗi:\n" + string.Join("\n", failed) : ""),
                        failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            };
            btnUpdate.Click += async (s, e) => await CheckUpdate(true);
            btnHome.Click += (s, e) => { try { Process.Start("https://github.com/" + App.Repo); } catch { } };
        }

        ListBox lstProtect;

        void RefreshProtectList()
        {
            if (lstProtect == null || lstProtect.IsDisposed) return;
            lstProtect.Items.Clear();
            foreach (var p in cfg.Protected) lstProtect.Items.Add(p);
            if (cfg.Protected.Count == 0) lstProtect.Items.Add(UiLanguage.L("(trống) — thêm thư mục / file ở bên phải, hoặc chuột phải một mục trong tab Dọn dẹp → \"Không bao giờ dọn mục này\"",
                                                                           "(empty) — add folders / files on the right, or right-click an item in Cleanup → \"Never clean this item\""));
        }

        void SaveAutoSettings(bool quiet)
        {
            cfg.AlertGB = (int)numAlert.Value;
            cfg.AutoClean = chkAutoClean.Checked;
            cfg.AutoDays = (int)numAutoDays.Value;
            cfg.ZaloAuto = chkZaloAuto.Checked;
            cfg.ZaloAutoDays = (int)numZaloDays.Value;
            cfg.ZaloAutoEvery = (int)numZaloEvery.Value;
            cfg.Save();
            try { Shortcut.SetStartup(chkStartup.Checked); } catch (Exception ex) { App.Log("Không đặt được chạy cùng Windows: " + ex.Message); }
            App.Log("Đã lưu thiết lập tự động.");
            if (quiet) return;
            string m = "Đã lưu thiết lập.";
            if (chkStartup.Checked) m += "\n\nTừ lần mở máy sau, app sẽ tự chạy ngầm và báo khi ổ còn dưới " + cfg.AlertGB + " GB.";
            if (chkAutoClean.Checked)
            {
                m += "\n\nRác hệ thống sẽ được tự dọn mỗi " + cfg.AutoDays + " ngày (khi app đang chạy ngầm).";
                if (!chkStartup.Checked) m += "\nLưu ý: cần bật chạy ngầm thì việc tự dọn mới diễn ra.";
            }
            if (chkZaloAuto.Checked)
                m += UiLanguage.L("\n\nẢnh, video, file Zalo cũ hơn " + cfg.ZaloAutoDays + " ngày sẽ được tự đưa lên Google Drive mỗi " + cfg.ZaloAutoEvery + " ngày" + (chkStartup.Checked ? "." : " — cần bật chạy ngầm."),
                                  "\n\nZalo files older than " + cfg.ZaloAutoDays + " days will be moved to Google Drive every " + cfg.ZaloAutoEvery + " days" + (chkStartup.Checked ? "." : " — background mode must be on."));
            Msg(m);
        }

        // ================================================================= tự kiểm tra khi đóng gói
        public string SelfTest()
        {
            var sb = new StringBuilder();
            UpdateDrives();
            if (tabs.TabPages.Count != 10) throw new Exception("Expected ten tabs");
            sb.Append("tabs=").Append(tabs.TabPages.Count);
            string expectedFirstTab = UiLanguage.English ? "Cleanup" : "Dọn dẹp";
            if (tabs.TabPages[0].Text != expectedFirstTab) throw new Exception("Language tab mismatch: " + tabs.TabPages[0].Text);
            string expectedMoved = UiLanguage.English ? "Moved files" : "Đã chuyển đi";
            string expectedSettings = UiLanguage.English ? "⚙ Settings" : "⚙ Cài đặt";
            if (tabs.TabPages[3].Text != "Smart Folder" ||
                tabs.TabPages[4].Text != expectedMoved ||
                tabs.TabPages[6].Text != UiLanguage.L("Gỡ phần mềm", "Uninstall") ||
                tabs.TabPages[9].Text != expectedSettings)
                throw new Exception("Navigation order or Settings translation mismatch.");
            if (btnSettingsVietnamese == null || btnSettingsEnglish == null ||
                btnSettingsVietnamese.IsDisposed || btnSettingsEnglish.IsDisposed ||
                !btnSettingsVietnamese.Text.Contains("Tiếng Việt") ||
                !btnSettingsEnglish.Text.Contains("English"))
                throw new Exception("Settings language controls missing.");
            var activeButton = UiLanguage.English ? btnSettingsEnglish : btnSettingsVietnamese;
            if (activeButton.FlatAppearance.BorderSize != 2)
                throw new Exception("Selected Settings language is not highlighted.");
            sb.Append(" settings=OK");
            if (UiLanguage.English && UiLanguage.T("Xóa vĩnh viễn") != "Delete permanently") throw new Exception("Safety translation mismatch");
            sb.Append(" language=").Append(UiLanguage.Code);
            smartList.Parent.PerformLayout();
            if (smartList.Top < 150) throw new Exception("Smart Folder list covers its toolbar (top=" + smartList.Top + ")");
            SmartFolders.SelfTest();
            sb.Append(" smart=OK");
            NativeShortcut.SelfTest();
            sb.Append(" shortcut=OK");
            var sources = Junk.Sources(cfg);
            sb.Append(" junk=").Append(sources.Count);
            var zr = Junk.ZaloRoots(cfg);
            sb.Append(" zaloRoots=").Append(zr.Count);
            string sys = Fs.SystemDrive + "\\";
            var checks = new Dictionary<string, Level>
            {
                { sys + "Windows", Level.Cam }, { sys + "pagefile.sys", Level.Cam }, { sys + "Program Files", Level.Cam },
                { Path.Combine(Fs.LocalAppData, "Temp"), Level.Rac }, { @"D:\Zalo Data\message", Level.Cam }
            };
            foreach (var kv in checks)
            {
                var got = PathRules.Get(kv.Key, !kv.Key.EndsWith(".sys")).Level;
                if (got != kv.Value) throw new Exception("PathRules sai: " + kv.Key + " = " + got);
            }
            if (!PathRules.IsProtected(sys + "Windows\\System32")) throw new Exception("IsProtected sai");
            var map = DiskMap.Scan(App.AppDir, null, CancellationToken.None);
            mapRoot = map;
            ShowMapDir(map);
            sb.Append(" map=").Append(map.Count);
            var d = DupFinder.Find(new[] { App.AppDir }, 1, null, CancellationToken.None);
            sb.Append(" dup=").Append(d.Count);
            LoadMovedList();
            sb.Append(" moved=").Append(movedList.Items.Count);
            var csv = MovedStore.ParseCsv("\"a\",\"b,c\"\r\n\"1\",\"x\"\"y\"\r\n");
            if (csv.Count != 2 || csv[0][1] != "b,c" || csv[1][1] != "x\"y") throw new Exception("CSV sai");
            var personal = new List<CleanItem> { new CleanItem { Category = "tu", Kind = "chuyen", Path = App.ExePath, Root = App.AppDir, Name = "x", Folder = App.AppDir, Size = 1 } };
            list.Items.Add(NewRow(personal[0], false));
            UpdateGroupHeaders(); UpdateSummary();
            list.Items.Clear();
            InstalledApps.SelfTest();
            allApps = InstalledApps.Scan(null);
            FillAppList();
            if (allApps.Count == 0) throw new Exception("No installed programs found");
            sb.Append(" apps=").Append(allApps.Count).Append("/").Append(allApps.Count(a => a.LastUsed.HasValue));
            string cardDir = Path.Combine(Path.GetTempPath(), "PCCleanerCardTest");
            string card = ShareCard.Create(37L * Fmt.GB + 512 * Fmt.MB, 12, cardDir);
            if (!File.Exists(card) || new FileInfo(card).Length < 20000) throw new Exception("Share card not created");
            try { Directory.Delete(cardDir, true); } catch { }
            sb.Append(" card=OK");
            if (UpdateInstaller.Sha256File(App.ExePath).Length != 64) throw new Exception("SHA-256 failed");
            sb.Append(" zaloOld=").Append(ZaloAuto.FindOld(cfg, 90, null).Count);
            UpdateStatsLabel(); UpdateZaloLastLabel();
            SelfTestTools(sb);
            var br = Browsers.Detect();
            FillBrowserList(br);
            UpdateBrowserSummary();
            sb.Append(" browsers=").Append(br.Select(x => x.Browser).Distinct().Count()).Append("/").Append(br.Count);
            if (UiLanguage.English)
            {
                string path = UiLanguage.P(@"D:\Thư mục công ty\Báo cáo.docx");
                string shown = UiLanguage.T("Thư mục: " + path);
                if (shown != "Folder: " + path) throw new Exception("Bộ dịch làm thay đổi đường dẫn của người dùng: " + shown);
                string warn = UiLanguage.T("Ổ C: sắp đầy!");
                if (warn != "Drive C: almost full!") throw new Exception("Dịch sai cảnh báo ổ đầy: " + warn);
                sb.Append(" EN_OK");
            }
            sb.Append(" UI_OK");
            return sb.ToString();
        }
    }
}
