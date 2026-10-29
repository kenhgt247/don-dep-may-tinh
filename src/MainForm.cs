// Dọn Dẹp Máy Tính — cửa sổ chính (5 tab).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = DonDep.LocalizedMessageBox;

namespace DonDep
{
    class CleanItem
    {
        public string Category;   // rac, zalo, tai, lon, tu
        public string Kind;       // xoa (rác) | chuyen (file cá nhân)
        public string Action = "chuyen"; // với file cá nhân: chuyen | xoa
        public string Path, Root, Name, Folder;
        public long Size;
        public DateTime? Date, Before;
        public bool Default = true;
    }

    partial class MainForm : Form
    {
        static readonly string[] CatOrder = { "rac", "zalo", "tai", "lon", "tu" };
        static readonly Dictionary<string, string> CatLabel = new Dictionary<string, string>
        { { "rac", "Rác hệ thống" }, { "zalo", "Zalo" }, { "tai", "Downloads" }, { "lon", "File lớn" }, { "tu", "Tự chọn" } };
        static readonly ISet<string> ImageExt = Fs.ExtSet(".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff");
        static readonly ISet<string> VideoExt = Fs.ExtSet(".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".mpg", ".mpeg", ".webm", ".3gp");
        static readonly ISet<string> LargeExt = Fs.ExtSet(".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".m4v", ".mpg", ".mpeg", ".webm",
            ".3gp", ".m2ts", ".mts", ".mp3", ".wav", ".flac", ".m4a", ".aac", ".zip", ".rar", ".7z", ".tar", ".gz", ".iso",
            ".psd", ".psb", ".tif", ".tiff", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".pdf", ".pptx", ".docx", ".xlsx", ".prproj", ".aep");
        static readonly string[] LargeSkip = { "$RECYCLE.BIN", "System Volume Information", "Windows", "Program Files", "Program Files (x86)",
            "ProgramData", "AppData", "Recovery", "MSOCache", "$WinREAgent", "WindowsApps", "Config.Msi", "SteamLibrary", "steamapps",
            "Epic Games", "Riot Games", "Garena", "XboxGames", "OneDrive*", "Google Drive", "My Drive", "DonDep*", "node_modules", ".git", "Zalo Data" };
        static readonly Color Yellow = Color.FromArgb(255, 222, 89);

        readonly Settings cfg = Settings.Load();
        readonly List<Control> busyControls = new List<Control>();
        bool suspendSummary;

        TabControl tabs;
        ToolStripStatusLabel lblStatus;
        IProgress<string> status;

        bool isBusy;

        public MainForm(string languageOverride = null)
        {
            UiLanguage.Code = languageOverride == "en" ? "en" : cfg.Language;
            Icon = App.AppIcon;
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(1180, 780);
            MinimumSize = new Size(1100, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BuildInterface();

            App.LogSink = line =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)(() => txtLog.AppendText(line + Environment.NewLine))); } catch { }
            };
            Shown += OnShownAsync;
            FormClosing += (s, e) => { SaveCleanSettings(); cfg.DupFolders = lstDupFolders.Items.Cast<string>().ToList(); cfg.Save(); App.LogSink = null; };
        }

        void BuildInterface()
        {
            Text = App.Title;
            busyControls.Clear();
            tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
            // Workflow first, settings always last. Keep this order in both languages.
            var tabClean = NewTab("Dọn dẹp");
            var tabMap = NewTab("Bản đồ ổ đĩa");
            var tabDup = NewTab("File trùng");
            var tabSmart = NewTab("Smart Folder");
            var tabMoved = NewTab("Đã chuyển đi");
            var tabBrowser = NewTab("Trình duyệt");
            var tabSettings = NewTab("⚙ Cài đặt");
            BuildCleanTab(tabClean);
            BuildMapTab(tabMap);
            BuildDupTab(tabDup);
            BuildSmartFolderTab(tabSmart);
            BuildMovedTab(tabMoved);
            BuildBrowserTab(tabBrowser);
            BuildAutoTab(tabSettings);

            var strip = new StatusStrip();
            lblStatus = new ToolStripStatusLabel(UiLanguage.T("Sẵn sàng.")) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            // Language is configured in Settings rather than taking space in the status bar.
            strip.Items.Add(lblStatus);
            Controls.Add(tabs);
            Controls.Add(strip);
            UiLanguage.Attach(tabs);
            status = new Progress<string>(s => { if (s.Length > 160) s = s.Substring(0, 157) + "..."; lblStatus.Text = UiLanguage.T(s); });
            tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedIndex == 4) LoadMovedList(); };
        }

        void SwitchLanguage(string language)
        {
            if (isBusy || (language != "vi" && language != "en") ||
                language == UiLanguage.Code) return;
            int tabIndex = tabs.SelectedIndex;
            SaveCleanSettings();
            cfg.DupFolders = lstDupFolders.Items.Cast<string>().ToList();
            // Keep unsaved settings visible across a language change. Only the
            // language itself is persisted; Windows startup and automation are untouched.
            int pendingAlert = (int)numAlert.Value;
            int pendingDays = (int)numAutoDays.Value;
            bool pendingAuto = chkAutoClean.Checked;
            bool pendingStartup = chkStartup.Checked;
            cfg.Language = language;
            cfg.Save();
            UiLanguage.Code = language;
            foreach (Control control in Controls.Cast<Control>().ToArray())
            {
                Controls.Remove(control);
                control.Dispose();
            }
            BuildInterface();
            numAlert.Value = pendingAlert;
            numAutoDays.Value = pendingDays;
            chkAutoClean.Checked = pendingAuto;
            chkStartup.Checked = pendingStartup;
            tabs.SelectedIndex = Math.Max(0, Math.Min(tabs.TabPages.Count - 1, tabIndex));
            UpdateDrives();
            UpdateDriveLabel(false);
            Status(language == "en" ? "Language changed to English." : "Đã chuyển sang tiếng Việt.");
        }

        TabPage NewTab(string t)
        {
            var p = new TabPage(t) { UseVisualStyleBackColor = true };
            tabs.TabPages.Add(p);
            return p;
        }

        // ------------------------------------------------------------ tiện ích giao diện
        Button Btn(string text, int x, int y, int w, int h, bool busy = true)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
            if (busy) busyControls.Add(b);
            return b;
        }
        static Label Lbl(string text, int x, int y, int w, int h)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(w, h) };
        }
        static NumericUpDown Num(Control parent, string label, int y, int min, int max, int val, int lx = 10, int lw = 250)
        {
            parent.Controls.Add(Lbl(label, lx, y + 3, lw, 22));
            var n = new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Min(max, Math.Max(min, val)), Location = new Point(lx + lw + 2, y), Size = new Size(76, 24) };
            parent.Controls.Add(n);
            return n;
        }

        void Msg(string text, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            MessageBox.Show(this, text, App.Name, MessageBoxButtons.OK, icon);
        }
        bool Confirm(string text)
        {
            return MessageBox.Show(this, text, App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }
        void SetBusy(bool busy)
        {
            isBusy = busy;
            foreach (var c in busyControls) c.Enabled = !busy;
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            if (!busy && smartList != null && !smartList.IsDisposed) { UpdateSmartSummary(); UpdateSmartUndoCount(); }
        }
        void Status(string s) { status.Report(s); }

        /// Chạy việc nặng ở luồng nền (STA, để thao tác Thùng rác của Windows chạy đúng).
        static Task RunSta(Action a)
        {
            var tcs = new TaskCompletionSource<bool>();
            var t = new Thread(() => { try { a(); tcs.SetResult(true); } catch (Exception ex) { tcs.SetException(ex); } });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            return tcs.Task;
        }

        async Task Guard(Func<Task> work)
        {
            try { await work(); }
            catch (Exception ex)
            {
                App.Log("LỖI: " + ex.Message);
                Msg("Có lỗi xảy ra: " + ex.Message, MessageBoxIcon.Error);
            }
            finally { suspendSummary = false; SetBusy(false); }
        }

        async void OnShownAsync(object sender, EventArgs e)
        {
            UpdateDrives();
            UpdateDriveLabel(false);
            App.Log("Dữ liệu, nhật ký và danh sách file đã chuyển nằm ở: " + App.DataDir);
            long keep = (long)numKeep.Value * Fmt.GB;
            var low = Fs.FixedDrives().Where(d => { try { return d.AvailableFreeSpace < keep; } catch { return false; } }).Select(d => d.Name).ToList();
            if (low.Count > 0) Status("Ổ sắp đầy: " + string.Join(", ", low) + " — bấm \"Quét máy\" để dọn.");
            await CheckUpdate(false);
        }

        // ================================================================= TAB 1: DỌN DẸP
        FlowLayoutPanel flowDrives;
        CheckBox chkJunk, chkZalo, chkDown, chkLarge;
        NumericUpDown numDays, numLarge, numKeep;
        ListView list;
        readonly Dictionary<string, ListViewGroup> groups = new Dictionary<string, ListViewGroup>();
        RadioButton rbDrive, rbFolder;
        Button btnConnect, btnScan, btnClean, btnAdmin;
        Label lblDrive, lblSum;
        TextBox txtDest, txtLog;
        PictureBox prevPic; Label prevLbl;
        ContextMenuStrip cleanMenu;
        ToolStripItem miSetMove, miSetDel;

        void BuildCleanTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 178 };
            var grpDrives = new GroupBox { Text = "Ổ đĩa", Location = new Point(6, 4), Size = new Size(360, 168) };
            flowDrives = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            grpDrives.Controls.Add(flowDrives);

            var grpScan = new GroupBox { Text = "Quét những gì", Location = new Point(374, 4), Size = new Size(420, 168) };
            Func<string, int, CheckBox> chk = (t, y) => new CheckBox { Text = t, Location = new Point(14, y), Size = new Size(396, 26), Checked = true };
            chkJunk = chk("Rác hệ thống (file tạm, bộ nhớ đệm, bản cập nhật cũ…)", 24);
            chkZalo = chk("Ảnh / video / file Zalo cũ", 56);
            chkDown = chk("Thư mục Downloads (file cũ)", 88);
            chkLarge = chk("File lớn (video, file nén, ISO…) — tự chọn từng file", 120);
            grpScan.Controls.AddRange(new Control[] { chkJunk, chkZalo, chkDown, chkLarge });

            var grpSet = new GroupBox { Text = "Thiết lập", Location = new Point(802, 4), Size = new Size(354, 168) };
            numDays = Num(grpSet, "Coi là \"cũ\" nếu quá (ngày):", 22, 7, 3650, cfg.Days);
            numLarge = Num(grpSet, "File lớn từ (MB):", 54, 50, 100000, cfg.LargeMB);
            numKeep = Num(grpSet, "Giữ trống tối thiểu (GB):", 86, 1, 2000, cfg.KeepGB);
            numKeep.ValueChanged += (s, e) => UpdateDrives();
            var btnZalo = Btn("Thêm thư mục Zalo khác…", 10, 122, 334, 30);
            btnZalo.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Chọn thư mục dữ liệu Zalo (ví dụ D:\\Zalo Data). App chỉ lấy ảnh, video, tài liệu — không đụng tới dữ liệu tin nhắn." })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    if (!cfg.ExtraZalo.Contains(dlg.SelectedPath, StringComparer.OrdinalIgnoreCase)) cfg.ExtraZalo.Add(dlg.SelectedPath);
                    cfg.Save();
                    App.Log("Đã thêm thư mục Zalo: " + dlg.SelectedPath);
                }
            };
            grpSet.Controls.Add(btnZalo);
            top.Controls.AddRange(new Control[] { grpDrives, grpScan, grpSet });

            list = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, ShowGroups = true, HideSelection = false };
            list.Columns.Add("Loại", 100);
            list.Columns.Add("Tên", 300);
            list.Columns.Add("Dung lượng", 90, HorizontalAlignment.Right);
            list.Columns.Add("Ngày sửa", 90);
            list.Columns.Add("Thư mục", 230);
            list.Columns.Add("Xử lý", 130);
            foreach (var c in CatOrder) { var g = new ListViewGroup(c, CatLabel[c]); list.Groups.Add(g); groups[c] = g; }
            list.ItemChecked += (s, e) => UpdateSummary();
            list.SelectedIndexChanged += (s, e) =>
            {
                if (list.SelectedItems.Count == 0) return;
                var o = (CleanItem)list.SelectedItems[0].Tag;
                if (o.Kind == "chuyen") ShowPreview(prevPic, prevLbl, o.Path);
                else { ShowPreview(prevPic, prevLbl, null); prevLbl.Text = o.Name + Environment.NewLine + Fmt.Size(o.Size); }
            };
            list.DoubleClick += (s, e) =>
            {
                if (list.SelectedItems.Count == 0) return;
                var o = (CleanItem)list.SelectedItems[0].Tag;
                if (o.Path != Junk.RecycleBin && o.Path != Junk.Hibernate) Fs.OpenInExplorer(o.Path);
            };
            cleanMenu = new ContextMenuStrip();
            miSetMove = cleanMenu.Items.Add("☁  Chuyển lên Drive / ổ ngoài");
            miSetDel = cleanMenu.Items.Add("🗑  Xóa vĩnh viễn");
            cleanMenu.Items.Add(new ToolStripSeparator());
            var miOpen = cleanMenu.Items.Add("Mở thư mục chứa file");
            cleanMenu.Opening += (s, e) =>
            {
                if (list.SelectedItems.Count == 0) { e.Cancel = true; return; }
                bool any = list.SelectedItems.Cast<ListViewItem>().Any(i => ((CleanItem)i.Tag).Kind == "chuyen");
                miSetMove.Enabled = any; miSetDel.Enabled = any;
            };
            miSetMove.Click += (s, e) => Status("Đã đặt " + SetItemsAction(list.SelectedItems.Cast<ListViewItem>(), "chuyen") + " file: chuyển lên Drive / ổ ngoài.");
            miSetDel.Click += (s, e) => Status("Đã đặt " + SetItemsAction(list.SelectedItems.Cast<ListViewItem>(), "xoa") + " file: xóa vĩnh viễn.");
            miOpen.Click += (s, e) => { if (list.SelectedItems.Count > 0) { var o = (CleanItem)list.SelectedItems[0].Tag; if (o.Path != Junk.RecycleBin && o.Path != Junk.Hibernate) Fs.OpenInExplorer(o.Path); } };
            list.ContextMenuStrip = cleanMenu;

            var prev = NewPreviewPanel(out prevPic, out prevLbl);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 236 };
            var grpDest = new GroupBox { Text = "File cần chuyển đi sẽ được lưu ở", Location = new Point(6, 4), Size = new Size(660, 108) };
            rbDrive = new RadioButton { Text = "Google Drive", Location = new Point(14, 26), Size = new Size(130, 24) };
            btnConnect = Btn("Kết nối Google Drive", 176, 22, 170, 30);
            lblDrive = Lbl("", 356, 28, 296, 22);
            rbFolder = new RadioButton { Text = "Ổ cứng ngoài / USB", Location = new Point(14, 66), Size = new Size(160, 24) };
            txtDest = new TextBox { Location = new Point(176, 66), Size = new Size(374, 24), ReadOnly = true, Text = cfg.DestFolder };
            var btnBrowse = Btn("Chọn…", 556, 63, 92, 30);
            grpDest.Controls.AddRange(new Control[] { rbDrive, btnConnect, lblDrive, rbFolder, txtDest, btnBrowse });
            if (cfg.DestMode == "folder") rbFolder.Checked = true; else rbDrive.Checked = true;
            rbDrive.CheckedChanged += (s, e) => { cfg.DestMode = rbDrive.Checked ? "drive" : "folder"; cfg.Save(); };
            btnBrowse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { Description = "Chọn thư mục trên ổ cứng ngoài / USB để lưu file chuyển đi" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    txtDest.Text = dlg.SelectedPath; rbFolder.Checked = true; cfg.DestFolder = dlg.SelectedPath; cfg.Save();
                }
            };
            btnConnect.Click += async (s, e) => await Guard(ConnectDrive);

            btnScan = Btn("Quét máy", 676, 10, 230, 46);
            btnClean = Btn("Dọn dẹp ngay", 914, 10, 240, 46);
            btnScan.Font = btnClean.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            btnClean.BackColor = Yellow;
            var btnAll = Btn("Chọn tất cả", 676, 66, 114, 32);
            var btnNone = Btn("Bỏ chọn hết", 796, 66, 114, 32);
            var btnLogs = Btn("Mở nhật ký", 916, 66, 114, 32, false);
            btnAdmin = Btn("Quyền admin", 1036, 66, 118, 32, false);
            if (App.IsAdmin) { btnAdmin.Enabled = false; btnAdmin.Text = "Đang là admin"; }
            lblSum = Lbl("Bấm \"Quét máy\" để bắt đầu.", 8, 118, 1140, 22);
            lblSum.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblSum.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtLog = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(6, 142), Size = new Size(1150, 88), Font = new Font("Consolas", 9f) };
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            bottom.Controls.AddRange(new Control[] { grpDest, btnScan, btnClean, btnAll, btnNone, btnLogs, btnAdmin, lblSum, txtLog });

            btnScan.Click += async (s, e) => await Guard(ScanAsync);
            btnClean.Click += async (s, e) => await Guard(CleanAsync);
            btnAll.Click += (s, e) => SetAllChecked(list, true);
            btnNone.Click += (s, e) => SetAllChecked(list, false);
            btnLogs.Click += (s, e) => { Directory.CreateDirectory(App.DataDir); Process.Start("explorer.exe", "\"" + App.DataDir + "\""); };
            btnAdmin.Click += (s, e) => RestartAsAdmin();

            tab.Controls.Add(list);
            tab.Controls.Add(prev);
            tab.Controls.Add(bottom);
            tab.Controls.Add(top);
        }

        void SaveCleanSettings()
        {
            cfg.Days = (int)numDays.Value; cfg.LargeMB = (int)numLarge.Value; cfg.KeepGB = (int)numKeep.Value;
            cfg.Save();
        }

        void UpdateDrives()
        {
            flowDrives.SuspendLayout();
            flowDrives.Controls.Clear();
            long keep = (long)numKeep.Value * Fmt.GB;
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable)) continue;
                    long total = d.TotalSize, free = d.AvailableFreeSpace;
                    int pct = total > 0 ? (int)(100 * (total - free) / total) : 0;
                    var lbl = new Label { AutoSize = false, Width = 330, Height = 19, Text = d.Name + "   còn trống " + Fmt.Size(free) + " / " + Fmt.Size(total) };
                    if (free < keep) { lbl.ForeColor = Color.Firebrick; lbl.Text += "   ⚠ sắp đầy"; }
                    var bar = new ProgressBar { Width = 330, Height = 10, Value = Math.Min(100, Math.Max(0, pct)), Margin = new Padding(3, 0, 3, 6) };
                    flowDrives.Controls.Add(lbl); flowDrives.Controls.Add(bar);
                }
                catch { }
            }
            flowDrives.ResumeLayout();
        }

        void UpdateSummary()
        {
            if (suspendSummary) return;
            int n = 0; long del = 0, mov = 0, perm = 0;
            foreach (ListViewItem it in list.CheckedItems)
            {
                var o = (CleanItem)it.Tag; n++;
                if (o.Kind == "xoa") del += o.Size; else if (o.Action == "xoa") perm += o.Size; else mov += o.Size;
            }
            lblSum.Text = "Đã chọn " + n + " mục   •   Xóa rác: " + Fmt.Size(del) + "   •   Xóa vĩnh viễn: " + Fmt.Size(perm) + "   •   Chuyển đi: " + Fmt.Size(mov);
        }

        void SetAllChecked(ListView lv, bool state)
        {
            suspendSummary = true;
            lv.BeginUpdate();
            foreach (ListViewItem it in lv.Items) it.Checked = state;
            lv.EndUpdate();
            suspendSummary = false;
            UpdateSummary();
        }

        static string ActionText(CleanItem o)
        {
            if (o.Kind == "xoa") return "🗑 Xóa (rác)";
            return o.Action == "xoa" ? "🗑 Xóa vĩnh viễn" : "☁ Chuyển đi";
        }

        ListViewItem NewRow(CleanItem o, bool check)
        {
            var it = new ListViewItem(UiLanguage.T(CatLabel[o.Category]));
            it.SubItems.Add(o.Kind == "xoa" ? UiLanguage.T(o.Name) : o.Name);
            it.SubItems.Add(Fmt.Size(o.Size));
            it.SubItems.Add(o.Date.HasValue ? o.Date.Value.ToString("dd/MM/yyyy") : "");
            it.SubItems.Add(o.Folder);
            it.SubItems.Add(UiLanguage.T(ActionText(o)));
            it.UseItemStyleForSubItems = false;
            it.SubItems[5].ForeColor = (o.Kind == "xoa" || o.Action == "xoa") ? Color.Firebrick : Color.RoyalBlue;
            it.Tag = o;
            it.Checked = check;
            it.Group = groups[o.Category];
            return it;
        }

        int SetItemsAction(IEnumerable<ListViewItem> items, string action)
        {
            int n = 0;
            foreach (var it in items.ToList())
            {
                var o = (CleanItem)it.Tag;
                if (o.Kind != "chuyen") continue;
                o.Action = action;
                it.SubItems[5].Text = UiLanguage.T(ActionText(o));
                it.SubItems[5].ForeColor = action == "xoa" ? Color.Firebrick : Color.RoyalBlue;
                n++;
            }
            UpdateSummary();
            return n;
        }

        void UpdateGroupHeaders()
        {
            foreach (var c in CatOrder)
            {
                int n = 0; long sum = 0;
                foreach (ListViewItem it in groups[c].Items) { n++; sum += ((CleanItem)it.Tag).Size; }
                string hint = c == "lon" ? "  — chưa chọn sẵn, hãy tự đánh dấu file muốn chuyển" : c == "tu" ? "  — thêm từ tab Bản đồ ổ đĩa" : "";
                groups[c].Header = UiLanguage.T(CatLabel[c] + ": " + n + " mục, " + Fmt.Size(sum) + hint);
            }
        }

        static string SuggestAction(string path)
        {
            var l = PathRules.Get(path, false).Level;
            return (l == Level.Rac || l == Level.AnToan) ? "xoa" : "chuyen";
        }

        async Task ScanAsync()
        {
            SetBusy(true);
            SaveCleanSettings();
            var keepTu = groups["tu"].Items.Cast<ListViewItem>().Select(i => Tuple.Create((CleanItem)i.Tag, i.Checked)).ToList();
            bool doJunk = chkJunk.Checked, doZalo = chkZalo.Checked, doDown = chkDown.Checked, doLarge = chkLarge.Checked;
            int days = (int)numDays.Value; long minLarge = (long)numLarge.Value * Fmt.MB;
            App.Log("Bắt đầu quét...");
            var rows = await Task.Run(() =>
            {
                var r = CatOrder.ToDictionary(c => c, c => new List<CleanItem>());
                var seen = new HashSet<string>(keepTu.Select(t => t.Item1.Path), StringComparer.OrdinalIgnoreCase);
                var before = DateTime.Now.AddDays(-days);
                Action<string, FileInfo, string> addFile = (cat, f, root) =>
                {
                    if (!seen.Add(f.FullName)) return;
                    r[cat].Add(new CleanItem { Category = cat, Kind = "chuyen", Path = f.FullName, Root = root, Size = f.Length, Name = f.Name, Folder = f.DirectoryName, Date = f.LastWriteTime, Action = SuggestAction(f.FullName) });
                };
                if (doJunk)
                {
                    foreach (var j in Junk.Sources(cfg))
                    {
                        status.Report("Đang tính: " + j.Name);
                        long size = Fs.FolderSize(j.Path, j.Before);
                        if (size > 0) r["rac"].Add(new CleanItem { Category = "rac", Kind = "xoa", Path = j.Path, Size = size, Name = j.Name, Folder = j.Path, Before = j.Before, Default = j.Default });
                    }
                    status.Report("Đang tính: Thùng rác");
                    long rb = Junk.RecycleBinSize();
                    if (rb > 0) r["rac"].Add(new CleanItem { Category = "rac", Kind = "xoa", Path = Junk.RecycleBin, Size = rb, Name = "Thùng rác (Recycle Bin)", Folder = "" });
                    long hib = Junk.HiberSize();
                    if (hib > 500 * Fmt.MB)
                    {
                        if (App.IsAdmin)
                            r["rac"].Add(new CleanItem { Category = "rac", Kind = "xoa", Path = Junk.Hibernate, Size = hib, Name = "Tắt chế độ ngủ đông (hiberfil.sys) — chỉ chọn nếu bạn không dùng Hibernate", Folder = Fs.SystemDrive + "\\hiberfil.sys", Default = false });
                        else App.Log("Gợi ý: file ngủ đông hiberfil.sys đang chiếm " + Fmt.Size(hib) + " trên ổ C. Bấm \"Quyền admin\" rồi quét lại nếu muốn tắt nó.");
                    }
                    if (!App.IsAdmin) App.Log("Mẹo: bấm \"Quyền admin\" để quét thêm gói cập nhật Windows cũ (thường vài GB).");
                }
                if (doZalo)
                {
                    bool found = false;
                    foreach (var z in Junk.ZaloRoots(cfg))
                    {
                        if (!Directory.Exists(z.Path)) continue;
                        found = true;
                        foreach (var f in Fs.Walk(z.Path, new[] { "sticker*", "emoji*", "avatar*", "*cache*", "thumb*", "*Thumb", "zinstant", "fileNoise" }, 0, before, z.Ext, status.Report))
                            addFile("zalo", f, z.Path);
                    }
                    if (!found) App.Log("Không tìm thấy thư mục Zalo. Nếu bạn đã chuyển dữ liệu Zalo sang chỗ khác, bấm \"Thêm thư mục Zalo khác\".");
                }
                if (doDown) foreach (var f in Fs.Walk(Fs.Downloads, null, 0, before, null, status.Report)) addFile("tai", f, Fs.Downloads);
                if (doLarge)
                {
                    var roots = new List<string> { Fs.UserProfile };
                    foreach (var d in Fs.FixedDrives()) if (!d.Name.TrimEnd('\\').Equals(Fs.SystemDrive, StringComparison.OrdinalIgnoreCase)) roots.Add(d.RootDirectory.FullName);
                    foreach (var root in roots)
                        foreach (var f in Fs.Walk(root, LargeSkip, minLarge, null, LargeExt, status.Report)) addFile("lon", f, root);
                }
                return r;
            });

            suspendSummary = true;
            list.BeginUpdate();
            list.Items.Clear();
            var defaults = new Dictionary<string, bool> { { "rac", true }, { "zalo", true }, { "tai", true }, { "lon", false } };
            foreach (var c in new[] { "rac", "zalo", "tai", "lon" })
            {
                var sorted = rows[c].OrderByDescending(o => o.Size).ToList();
                list.Items.AddRange(sorted.Select(o => NewRow(o, defaults[c] && o.Default)).ToArray());
                App.Log(CatLabel[c] + ": " + sorted.Count + " mục, " + Fmt.Size(sorted.Sum(o => o.Size)));
            }
            foreach (var t in keepTu) list.Items.Add(NewRow(t.Item1, t.Item2));
            UpdateGroupHeaders();
            list.EndUpdate();
            suspendSummary = false;
            UpdateSummary();
            UpdateDrives();
            Status("Quét xong. Hãy xem danh sách, bỏ chọn những gì muốn giữ, rồi bấm \"Dọn dẹp ngay\".");
        }

        async Task CleanAsync()
        {
            var checkedItems = list.CheckedItems.Cast<ListViewItem>().ToList();
            var all = checkedItems.Select(i => (CleanItem)i.Tag).ToList();
            if (all.Count == 0) { Msg("Bạn chưa chọn mục nào. Hãy bấm \"Quét máy\" rồi đánh dấu những gì muốn dọn."); return; }
            var junk = all.Where(o => o.Kind == "xoa").ToList();
            var personal = all.Where(o => o.Kind == "chuyen").ToList();
            if (personal.Count > 0)
            {
                string ans = AskPersonalAction(personal);
                if (ans == null) { Status("Đã hủy. Chuột phải vào file để chọn chuyển đi hay xóa vĩnh viễn."); return; }
                if (ans == "move") SetItemsAction(checkedItems, "chuyen");
                if (ans == "delete") SetItemsAction(checkedItems, "xoa");
            }
            var moves = personal.Where(o => o.Action != "xoa").ToList();
            var perms = personal.Where(o => o.Action == "xoa").ToList();
            long delBytes = junk.Sum(o => o.Size), movBytes = moves.Sum(o => o.Size), permBytes = perms.Sum(o => o.Size);
            long keep = (long)numKeep.Value * Fmt.GB;
            bool useDrive = rbDrive.Checked;
            string destText = "", destBase = "";

            SetBusy(true);
            if (moves.Count > 0)
            {
                if (useDrive)
                {
                    if (!Rclone.Ready) { Msg("Hãy bấm \"Kết nối Google Drive\" trước, hoặc chọn \"Ổ cứng ngoài / USB\".", MessageBoxIcon.Warning); return; }
                    Status("Đang kiểm tra dung lượng Google Drive...");
                    double free = await Task.Run(() => Rclone.Free());
                    if (free >= 0)
                    {
                        double after = free - movBytes;
                        if (after < 500 * Fmt.MB) { Msg("Google Drive không đủ chỗ.\n\nCòn trống: " + Fmt.Size(free) + "\nCần: " + Fmt.Size(movBytes) + "\n\nHãy bỏ bớt file hoặc chọn ổ cứng ngoài.", MessageBoxIcon.Warning); Status("Sẵn sàng."); return; }
                        destText = "Google Drive (còn trống " + Fmt.Size(free) + " → sau khi chuyển còn " + Fmt.Size(after) + ")";
                    }
                    else destText = "Google Drive (không đọc được dung lượng còn trống — file nào không tải lên được sẽ được giữ nguyên trên máy)";
                }
                else
                {
                    string dest = txtDest.Text.Trim();
                    if (dest.Length == 0 || !Directory.Exists(dest)) { Msg("Hãy bấm \"Chọn…\" để chọn thư mục trên ổ cứng ngoài / USB.", MessageBoxIcon.Warning); return; }
                    string destRoot = Fs.Root(dest);
                    int same = moves.Count(o => Fs.Root(o.Path) == destRoot);
                    if (same > 0) { Msg("Thư mục đích nằm trên cùng ổ " + destRoot + " với " + same + " file đang chọn.\nChuyển trong cùng một ổ không giải phóng được dung lượng.\n\nHãy chọn ổ khác (ổ cứng ngoài / USB) hoặc bỏ chọn các file đó.", MessageBoxIcon.Warning); return; }
                    long destFree = Fs.DriveFree(destRoot);
                    long after = destFree - movBytes;
                    if (after < keep) { Msg("Ổ " + destRoot + " không đủ chỗ an toàn.\n\nCòn trống: " + Fmt.Size(destFree) + "\nCần: " + Fmt.Size(movBytes) + "\nSau khi chuyển sẽ còn: " + Fmt.Size(Math.Max(0, after)) + "\nMức giữ trống tối thiểu: " + Fmt.Size(keep) + "\n\nHãy bỏ bớt file hoặc chọn ổ khác.", MessageBoxIcon.Warning); return; }
                    destBase = Path.Combine(dest, "DonDep", App.PcName);
                    destText = destBase + " (ổ " + destRoot + " còn " + Fmt.Size(destFree) + " → sau khi chuyển còn " + Fmt.Size(after) + ")";
                }
            }

            var freeBefore = Fs.FreeMap();
            var gain = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in all)
            {
                if (o.Path == Junk.RecycleBin) continue;
                string root = o.Path == Junk.Hibernate ? Fs.SystemDrive.ToUpperInvariant() + "\\" : Fs.Root(o.Path);
                long g; gain.TryGetValue(root, out g); gain[root] = g + o.Size;
            }
            string estimate = "";
            foreach (var k in gain.Keys.OrderBy(x => x)) { long f; if (freeBefore.TryGetValue(k, out f)) estimate += "\n    " + k + "  còn " + Fmt.Size(f) + "  →  khoảng " + Fmt.Size(f + gain[k]); }

            string msg = "Bạn sắp:\n";
            if (junk.Count > 0) msg += "\n•  XÓA VĨNH VIỄN " + Fmt.Size(delBytes) + " rác (" + junk.Count + " mục)";
            if (junk.Any(o => o.Path == Junk.Hibernate)) msg += "\n•  TẮT chế độ ngủ đông (Hibernate) của Windows";
            if (perms.Count > 0)
            {
                msg += "\n•  XÓA VĨNH VIỄN " + perms.Count + " file cá nhân (" + Fmt.Size(permBytes) + ") — KHÔNG lấy lại được:";
                foreach (var o in perms.OrderByDescending(x => x.Size).Take(6)) msg += "\n      " + o.Name + " (" + Fmt.Size(o.Size) + ")";
                if (perms.Count > 6) msg += "\n      ... và " + (perms.Count - 6) + " file khác";
            }
            if (moves.Count > 0)
            {
                msg += "\n•  CHUYỂN " + moves.Count + " file (" + Fmt.Size(movBytes) + ") tới:\n    " + destText;
                msg += "\n    File chỉ bị xóa khỏi máy sau khi đã chép xong và kiểm tra đúng. Có thể lấy lại ở tab \"Đã chuyển đi\".";
            }
            if (estimate.Length > 0) msg += "\n\nDung lượng ước tính sau khi dọn:" + estimate;
            if (moves.Any(o => o.Category == "zalo")) msg += "\n\nLưu ý: ảnh/file Zalo đã chuyển sẽ không mở được trong Zalo trên máy này nữa.";
            msg += "\n\nTiếp tục?";
            if (!Confirm(msg)) { Status("Đã hủy."); return; }
            if (perms.Count > 0 && !Confirm("Xác nhận lần nữa: XÓA VĨNH VIỄN " + perms.Count + " file cá nhân (" + Fmt.Size(permBytes) + ")?\n\nCác file này sẽ không vào Thùng rác và không lấy lại được.")) { Status("Đã hủy."); return; }
            if (personal.Any(o => o.Category == "zalo"))
            {
                while (Junk.ZaloRunning())
                {
                    if (MessageBox.Show(this, "Zalo đang mở. Hãy tắt hẳn Zalo (kể cả biểu tượng nhỏ ở góc phải thanh Taskbar) rồi bấm Retry.", App.Name, MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry)
                    { Status("Đã hủy."); return; }
                }
            }
            if (junk.Any(o => o.Name.Contains("Zalo")) && Junk.ZaloRunning()) App.Log("Zalo đang mở nên một phần bộ nhớ đệm Zalo sẽ được bỏ qua. Tắt Zalo rồi dọn lại để xóa hết.");

            App.Log("===== Bắt đầu dọn dẹp =====");
            var deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var movedEntries = new List<MovedEntry>();
            await Task.Run(() =>
            {
                foreach (var o in junk) { status.Report("Đang xóa: " + o.Name); Junk.Remove(o.Path, o.Name, o.Before); }
                if (junk.Count > 0) App.Log("Đã dọn " + junk.Count + " mục rác.");
                foreach (var o in perms)
                {
                    status.Report("Đang xóa: " + o.Name);
                    try
                    {
                        if (PathRules.IsProtected(o.Path)) throw new Exception("Thư mục được bảo vệ.");
                        var fi = new FileInfo(o.Path);
                        if (fi.Exists) { fi.Attributes = FileAttributes.Normal; fi.Delete(); }
                        deleted.Add(o.Path);
                        App.Log("Đã xóa vĩnh viễn: " + o.Path + " (" + Fmt.Size(o.Size) + ")");
                    }
                    catch (Exception ex) { App.Log("Không xóa được " + o.Path + ": " + ex.Message); }
                }
                if (perms.Count > 0) App.Log("Đã xóa vĩnh viễn " + deleted.Count + "/" + perms.Count + " file cá nhân.");
                if (moves.Count > 0)
                {
                    movedEntries = useDrive ? MoveToDrive(moves) : MoveToFolder(moves, destBase);
                    int failed = moves.Count - movedEntries.Count;
                    App.Log("Đã chuyển " + movedEntries.Count + "/" + moves.Count + " file." + (failed > 0 ? " " + failed + " file không chuyển được, vẫn giữ nguyên trên máy." : ""));
                    if (movedEntries.Count > 0) App.Log("Danh sách file đã chuyển: " + MovedStore.Save(movedEntries));
                }
            });

            var movedSet = new HashSet<string>(movedEntries.Select(m => m.Orig), StringComparer.OrdinalIgnoreCase);
            suspendSummary = true;
            list.BeginUpdate();
            foreach (var it in checkedItems)
            {
                var o = (CleanItem)it.Tag;
                if (o.Kind == "xoa" || movedSet.Contains(o.Path) || deleted.Contains(o.Path)) list.Items.Remove(it);
            }
            UpdateGroupHeaders();
            list.EndUpdate();
            suspendSummary = false;
            UpdateSummary();
            var freeAfter = Fs.FreeMap();
            string report = "";
            foreach (var k in freeAfter.Keys.OrderBy(x => x)) { long b; if (freeBefore.TryGetValue(k, out b) && freeAfter[k] - b > 10 * Fmt.MB) report += "\n    " + k + "  +" + Fmt.Size(freeAfter[k] - b) + "  (nay còn trống " + Fmt.Size(freeAfter[k]) + ")"; }
            UpdateDrives();
            App.Log("===== Xong =====");
            Status("Đã dọn xong.");
            string done = "Đã dọn xong!";
            if (report.Length > 0) done += "\n\nĐã giải phóng:" + report;
            if (moves.Count > 0)
            {
                done += "\n\nĐã chuyển " + movedEntries.Count + "/" + moves.Count + " file.";
                if (moves.Count > movedEntries.Count) done += " File nào chưa chuyển được vẫn còn nguyên trên máy (xem nhật ký).";
            }
            Msg(done);
        }

        static MovedEntry Entry(CleanItem o, string dest, string code)
        {
            return new MovedEntry { Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Category = CatLabel[o.Category], Orig = o.Path, Dest = dest, SizeText = Fmt.Size(o.Size), Code = code, Bytes = o.Size };
        }

        List<MovedEntry> MoveToFolder(List<CleanItem> items, string destBase)
        {
            var moved = new List<MovedEntry>();
            int i = 0;
            foreach (var o in items)
            {
                i++;
                string target = Path.Combine(destBase, Fs.SafeName(o.Root), Fs.RelPath(o.Root, o.Path));
                try
                {
                    if (!File.Exists(o.Path)) continue;
                    status.Report("Đang chép " + i + "/" + items.Count + ": " + o.Name + " (" + Fmt.Size(o.Size) + ")");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    long srcLen = new FileInfo(o.Path).Length;
                    bool already = File.Exists(target) && new FileInfo(target).Length == srcLen;
                    if (!already) File.Copy(o.Path, target, true);
                    if (new FileInfo(target).Length != srcLen) throw new IOException("Bản sao không đủ dung lượng, giữ nguyên file gốc.");
                    File.SetAttributes(o.Path, FileAttributes.Normal);
                    File.Delete(o.Path);
                    moved.Add(Entry(o, target, target));
                }
                catch (Exception ex) { App.Log("Không chuyển được " + o.Path + ": " + ex.Message); }
            }
            return moved;
        }

        List<MovedEntry> MoveToDrive(List<CleanItem> items)
        {
            var moved = new List<MovedEntry>();
            foreach (var g in items.GroupBy(o => o.Root, StringComparer.OrdinalIgnoreCase))
            {
                string root = g.Key;
                string remotePath = "DonDep/" + App.PcName + "/" + Fs.SafeName(root);
                string remote = App.RemoteName + ":" + remotePath;
                var rel = g.Select(o => Fs.RelPath(root, o.Path).Replace('\\', '/')).ToList();
                App.Log("Tải lên Google Drive: " + rel.Count + " file từ " + root);
                Rclone.Move(root, rel, remote, status.Report);
                foreach (var o in g)
                {
                    if (File.Exists(o.Path)) continue;
                    string r = Fs.RelPath(root, o.Path).Replace('\\', '/');
                    moved.Add(Entry(o, "Google Drive/" + remotePath + "/" + r, remote + "/" + r));
                }
            }
            return moved;
        }

        async Task ConnectDrive()
        {
            SetBusy(true);
            if (!File.Exists(Rclone.Exe))
            {
                if (!Confirm("Để dùng Google Drive, app cần công cụ miễn phí rclone (khoảng 25 MB) từ trang chính thức rclone.org.\n\nTải về ngay?")) return;
                string err = await Task.Run(() => Rclone.Install(status.Report));
                if (err != null) { Msg(err, MessageBoxIcon.Warning); return; }
            }
            Msg("Trình duyệt sẽ mở trang đăng nhập Google.\n\n1. Chọn tài khoản Google muốn dùng để lưu file.\n2. Bấm Cho phép (Allow).\n\nApp chỉ được truy cập những file do chính nó tải lên, không đọc được các file khác trong Drive của bạn.");
            Status("Đang chờ bạn đăng nhập Google trong trình duyệt...");
            bool ok = await Task.Run(() => Rclone.Connect());
            if (!ok) Msg("Chưa kết nối được Google Drive. Hãy thử lại và nhớ bấm Cho phép trong trình duyệt.", MessageBoxIcon.Warning);
            await UpdateDriveLabelAsync(ok);
            Status("Sẵn sàng.");
        }

        void UpdateDriveLabel(bool withQuota) { var _ = UpdateDriveLabelAsync(withQuota); }

        async Task UpdateDriveLabelAsync(bool withQuota)
        {
            if (Rclone.Ready)
            {
                btnConnect.Text = "Kết nối lại";
                lblDrive.Text = "Đã kết nối";
                lblDrive.ForeColor = Color.DarkGreen;
                if (withQuota)
                {
                    double free = await Task.Run(() => Rclone.Free());
                    if (free >= 0) lblDrive.Text = "Đã kết nối — còn trống " + Fmt.Size(free);
                }
            }
            else
            {
                btnConnect.Text = "Kết nối Google Drive";
                lblDrive.Text = "Chưa kết nối";
                lblDrive.ForeColor = Color.DimGray;
            }
        }

        void RestartAsAdmin()
        {
            if (App.IsAdmin) { Msg("App đang chạy với quyền quản trị rồi."); return; }
            try
            {
                Process.Start(new ProcessStartInfo(App.ExePath) { UseShellExecute = true, Verb = "runas" });
                Close();
            }
            catch { App.Log("Đã hủy chạy với quyền quản trị."); }
        }

        // ---- hộp hỏi: chuyển lên Drive hay xóa vĩnh viễn
        string AskPersonalAction(List<CleanItem> items)
        {
            int nMove = items.Count(o => o.Action != "xoa"), nDel = items.Count - nMove;
            long bMove = items.Where(o => o.Action != "xoa").Sum(o => o.Size), bDel = items.Where(o => o.Action == "xoa").Sum(o => o.Size);
            long total = bMove + bDel;
            string result = null;
            using (var f = new Form())
            {
                f.Text = "Chuyển đi hay xóa vĩnh viễn?";
                f.Icon = Icon; f.Font = Font;
                f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false; f.MinimizeBox = false;
                f.StartPosition = FormStartPosition.CenterParent; f.ShowInTaskbar = false;
                var head = Lbl("Bạn đang chọn " + items.Count + " file cá nhân (" + Fmt.Size(total) + ").", 16, 12, 590, 26);
                head.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                var body = Lbl("• File còn cần dùng (ảnh, video, tài liệu quan trọng) → nên CHUYỂN lên Drive / ổ ngoài, lấy lại được bất cứ lúc nào.\n" +
                               "• File không cần nữa (file cài đặt, phim đã xem, file nén đã giải nén...) → XÓA VĨNH VIỄN cho nhanh, không tốn chỗ trên Drive.\n\n" +
                               "Hiện đang đặt:  ☁ chuyển đi " + nMove + " file (" + Fmt.Size(bMove) + ")   •   🗑 xóa vĩnh viễn " + nDel + " file (" + Fmt.Size(bDel) + ")\n" +
                               "Muốn chọn riêng từng file: bấm Quay lại, rồi chuột phải vào file trong danh sách.", 16, 44, 590, 120);
                f.Controls.Add(head); f.Controls.Add(body);
                int y = 170;
                Action<string, string, Color?> mk = (text, val, color) =>
                {
                    var b = new Button { Text = text, Location = new Point(16, y), Size = new Size(588, 36), Tag = val };
                    if (color.HasValue) b.BackColor = color.Value;
                    b.Click += (s, e) => { result = (string)((Button)s).Tag; f.Close(); };
                    f.Controls.Add(b);
                    y += 40;
                };
                if (nMove > 0 && nDel > 0) mk("Giữ như đã đặt (chuyển " + nMove + ", xóa " + nDel + ")", "keep", Yellow);
                mk("☁  Chuyển TẤT CẢ lên Drive / ổ ngoài (" + Fmt.Size(total) + ")", "move", null);
                mk("🗑  XÓA VĨNH VIỄN tất cả (" + Fmt.Size(total) + ") — không lấy lại được", "delete", null);
                mk("Quay lại", null, null);
                f.ClientSize = new Size(620, y + 8);
                f.ShowDialog(this);
            }
            return result;
        }

        // ---- xem trước ảnh
        Panel NewPreviewPanel(out PictureBox pic, out Label lbl)
        {
            var panel = new Panel { Dock = DockStyle.Right, Width = 250, Padding = new Padding(6) };
            pic = new PictureBox { Dock = DockStyle.Top, Height = 200, SizeMode = PictureBoxSizeMode.CenterImage, BackColor = Color.FromArgb(245, 245, 245) };
            lbl = new Label { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
            var head = new Label { Text = "Xem trước", Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            panel.Controls.Add(lbl); panel.Controls.Add(pic); panel.Controls.Add(head);
            return panel;
        }

        static void ShowPreview(PictureBox pic, Label lbl, string path)
        {
            if (pic.Image != null) { var old = pic.Image; pic.Image = null; old.Dispose(); }
            lbl.Text = "";
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            var fi = new FileInfo(path);
            lbl.Text = fi.Name + Environment.NewLine + Fmt.Size(fi.Length) + " • " + fi.LastWriteTime.ToString("dd/MM/yyyy");
            if (ImageExt.Contains(fi.Extension) && fi.Length < 40 * Fmt.MB)
            {
                try
                {
                    using (var fs = File.OpenRead(path))
                    using (var img = Image.FromStream(fs, false, false))
                    {
                        double scale = Math.Min(1.0, Math.Min((double)pic.Width / img.Width, (double)pic.Height / img.Height));
                        int tw = Math.Max(1, (int)(img.Width * scale)), th = Math.Max(1, (int)(img.Height * scale));
                        var bmp = new Bitmap(tw, th);
                        using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(img, 0, 0, tw, th); }
                        pic.Image = bmp;
                    }
                }
                catch { lbl.Text += Environment.NewLine + "(không xem trước được)"; }
            }
            else if (VideoExt.Contains(fi.Extension)) lbl.Text += Environment.NewLine + Environment.NewLine + "🎬 Video — bấm đúp để mở thư mục, rồi mở file để xem.";
            else lbl.Text += Environment.NewLine + Environment.NewLine + "Bấm đúp để mở thư mục chứa file.";
        }
    }
}
