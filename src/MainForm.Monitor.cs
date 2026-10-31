// Dọn Dẹp Máy Tính — tab Hiệu năng (theo dõi trực tiếp) và tab Lịch sử.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DonDep
{
    partial class MainForm
    {
        // ================================================================= HIỆU NĂNG
        Label lblCpu, lblRam, lblNet, lblUp, lblTip;
        ProgressBar barCpu, barRam;
        FlowLayoutPanel monDrives;
        ListView procList;
        Timer monTimer;
        int monTick;

        void BuildMonitorTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 214 };
            Func<string, int, Label> title = (t, x) => { var l = Lbl(t, x, 10, 260, 22); l.Font = new Font("Segoe UI", 10f, FontStyle.Bold); l.ForeColor = Color.FromArgb(31, 56, 95); top.Controls.Add(l); return l; };
            Func<int, Label> big = x => { var l = Lbl("…", x, 34, 270, 34); l.Font = new Font("Segoe UI", 16f, FontStyle.Bold); top.Controls.Add(l); return l; };
            title("CPU", 12); lblCpu = big(12);
            barCpu = new ProgressBar { Location = new Point(14, 72), Size = new Size(250, 14) }; top.Controls.Add(barCpu);
            title("RAM", 300); lblRam = big(300);
            barRam = new ProgressBar { Location = new Point(302, 72), Size = new Size(250, 14) }; top.Controls.Add(barRam);
            title(UiLanguage.L("Mạng", "Network"), 588); lblNet = big(588);
            title(UiLanguage.L("Máy đã bật", "Uptime"), 876); lblUp = big(876);
            monDrives = new FlowLayoutPanel { Location = new Point(10, 96), Size = new Size(1150, 52), FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            top.Controls.Add(monDrives);
            lblTip = Lbl("", 12, 150, 1150, 26);
            lblTip.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblTip.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            var btnKill = Btn(UiLanguage.L("Đóng chương trình đã chọn", "Close selected program"), 12, 178, 230, 32);
            var hint = Lbl(UiLanguage.L("Danh sách gộp theo chương trình (ví dụ Chrome gồm nhiều tiến trình). Cập nhật mỗi 2 giây. Không cho đóng thành phần của Windows.",
                                        "Grouped by program (e.g. Chrome has many processes). Updates every 2 seconds. Windows components cannot be closed."), 252, 186, 900, 22);
            top.Controls.AddRange(new Control[] { lblTip, btnKill, hint });

            procList = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, MultiSelect = false, HideSelection = false };
            procList.Columns.Add(UiLanguage.L("Chương trình", "Program"), 360);
            procList.Columns.Add(UiLanguage.L("Số tiến trình", "Processes"), 110, HorizontalAlignment.Right);
            procList.Columns.Add("RAM", 130, HorizontalAlignment.Right);
            procList.Columns.Add("CPU %", 100, HorizontalAlignment.Right);
            tab.Controls.Add(procList);
            tab.Controls.Add(top);

            btnKill.Click += (s, e) =>
            {
                if (procList.SelectedItems.Count == 0) { Msg(UiLanguage.L("Hãy chọn một chương trình trong danh sách.", "Select a program in the list.")); return; }
                string name = (string)procList.SelectedItems[0].Tag;
                if (SysMon.IsCritical(name)) { Msg(UiLanguage.L("\"" + name + "\" là thành phần của Windows hoặc của app này — không đóng được.", "\"" + name + "\" is part of Windows or this app and can't be closed."), MessageBoxIcon.Warning); return; }
                if (!Confirm(UiLanguage.P(UiLanguage.L("Đóng hẳn \"" + name + "\"?\n\nDữ liệu chưa lưu trong chương trình này sẽ bị mất.", "Close \"" + name + "\" completely?\n\nAny unsaved work in it will be lost.")))) return;
                int n = SysMon.Kill(name);
                App.Log("Đã đóng " + n + " tiến trình " + name);
                Status(UiLanguage.L("Đã đóng " + name + ".", "Closed " + name + "."));
            };

            monTimer = new Timer { Interval = 2000 };
            monTimer.Tick += (s, e) => MonitorTick();
            tabs.SelectedIndexChanged += (s, e) =>
            {
                bool on = tabs.SelectedTab == tab;
                if (on) { MonitorTick(); monTimer.Start(); } else monTimer.Stop();
            };
            FormClosed += (s, e) => { try { monTimer.Stop(); monTimer.Dispose(); } catch { } };
        }

        void MonitorTick()
        {
            if (IsDisposed || procList == null || procList.IsDisposed) return;
            double cpu = SysMon.Cpu();
            ulong total, avail; int load;
            SysMon.Memory(out total, out avail, out load);
            double net = SysMon.NetSpeed();
            lblCpu.Text = cpu.ToString("0") + " %";
            barCpu.Value = (int)Math.Max(0, Math.Min(100, cpu));
            lblRam.Text = Fmt.Size((long)(total - avail)) + " / " + Fmt.Size((long)total);
            barRam.Value = Math.Max(0, Math.Min(100, load));
            lblNet.Text = Fmt.Size((long)net) + "/s";
            var up = SysMon.Uptime();
            lblUp.Text = (up.Days > 0 ? up.Days + UiLanguage.L(" ngày ", "d ") : "") + up.Hours + "h" + up.Minutes.ToString("00");

            if (monTick++ % 5 == 0)
            {
                monDrives.SuspendLayout();
                monDrives.Controls.Clear();
                foreach (var d in Fs.FixedDrives())
                {
                    try
                    {
                        long t = d.TotalSize, f = d.AvailableFreeSpace;
                        int pct = t > 0 ? (int)(100 * (t - f) / t) : 0;
                        var p = new Panel { Width = 270, Height = 44, Margin = new Padding(2, 0, 16, 0) };
                        var l = new Label { Text = d.Name.TrimEnd('\\') + "  " + UiLanguage.L("còn trống ", "free ") + Fmt.Size(f) + " / " + Fmt.Size(t), Location = new Point(0, 2), Size = new Size(270, 20), UseMnemonic = false };
                        if (f < cfg.AlertGB * Fmt.GB) l.ForeColor = Color.Firebrick;
                        var b = new ProgressBar { Location = new Point(0, 24), Size = new Size(260, 12), Value = Math.Max(0, Math.Min(100, pct)) };
                        p.Controls.Add(l); p.Controls.Add(b);
                        monDrives.Controls.Add(p);
                    }
                    catch { }
                }
                monDrives.ResumeLayout();
            }

            var procs = SysMon.Processes();
            var top = procs.OrderByDescending(p => p.Cpu * 50 * Fmt.MB + p.Ram).Take(60).ToList();
            string sel = procList.SelectedItems.Count > 0 ? (string)procList.SelectedItems[0].Tag : null;
            procList.BeginUpdate();
            procList.Items.Clear();
            foreach (var p in top)
            {
                var it = new ListViewItem(p.Name) { Tag = p.Name };
                it.SubItems.Add(p.Count.ToString());
                it.SubItems.Add(Fmt.Size(p.Ram));
                it.SubItems.Add(p.Cpu.ToString("0.0"));
                if (SysMon.IsCritical(p.Name)) it.ForeColor = Color.DimGray;
                procList.Items.Add(it);
                if (p.Name == sel) it.Selected = true;
            }
            procList.EndUpdate();

            var heavyRam = procs.Where(p => !SysMon.IsCritical(p.Name)).OrderByDescending(p => p.Ram).FirstOrDefault();
            var heavyCpu = procs.Where(p => !SysMon.IsCritical(p.Name)).OrderByDescending(p => p.Cpu).FirstOrDefault();
            string tip;
            Color color = Color.SeaGreen;
            if (load >= 85 && heavyRam != null) { tip = UiLanguage.L("⚠ RAM gần đầy (" + load + "%). Nặng nhất: " + heavyRam.Name + " (" + Fmt.Size(heavyRam.Ram) + ") — đóng bớt cửa sổ / tab không dùng.", "⚠ Memory almost full (" + load + "%). Heaviest: " + heavyRam.Name + " (" + Fmt.Size(heavyRam.Ram) + ") — close unused windows / tabs."); color = Color.Firebrick; }
            else if (cpu >= 85 && heavyCpu != null) { tip = UiLanguage.L("⚠ CPU đang rất bận (" + cpu.ToString("0") + "%). Nặng nhất: " + heavyCpu.Name + ".", "⚠ CPU is very busy (" + cpu.ToString("0") + "%). Heaviest: " + heavyCpu.Name + "."); color = Color.DarkOrange; }
            else if (Fs.FixedDrives().Any(d => { try { return d.AvailableFreeSpace < cfg.AlertGB * Fmt.GB; } catch { return false; } })) { tip = UiLanguage.L("⚠ Có ổ đĩa sắp đầy — vào tab Dọn dẹp để giải phóng chỗ.", "⚠ A drive is almost full — use the Cleanup tab to free space."); color = Color.Firebrick; }
            else tip = UiLanguage.L("✓ Máy đang chạy bình thường.", "✓ Your PC is running normally.");
            lblTip.Text = tip; lblTip.ForeColor = color;
        }

        // ================================================================= kiểm tra tự động
        void SelfTestTools(System.Text.StringBuilder sb)
        {
            // Danh sách bảo vệ
            string root = Path.Combine(Path.GetTempPath(), "PCCleanerProtectTest_" + Guid.NewGuid().ToString("N"));
            string keep = Path.Combine(root, "giu lai"), junk = Path.Combine(root, "rac");
            Directory.CreateDirectory(keep); Directory.CreateDirectory(junk);
            File.WriteAllText(Path.Combine(keep, "a.txt"), "x"); File.WriteAllText(Path.Combine(junk, "b.txt"), "x");
            var saved = cfg.Protected.ToList();
            try
            {
                cfg.Protected = new List<string> { keep };
                cfg.Save();
                if (!Protect.Has(Path.Combine(keep, "a.txt")) || Protect.Has(Path.Combine(junk, "b.txt")) || !Protect.Contains(root)) throw new Exception("Protect matching");
                Fs.RemoveTreeSafe(root, DateTime.MaxValue);
                if (!File.Exists(Path.Combine(keep, "a.txt")) || Directory.Exists(junk)) throw new Exception("Protected file was deleted or junk kept");
            }
            finally { cfg.Protected = saved; cfg.Save(); try { Directory.Delete(root, true); } catch { } }
            sb.Append(" protect=OK");

            // File cài đặt thừa
            string inst = Path.Combine(Path.GetTempPath(), "PCCleanerInstTest_" + Guid.NewGuid().ToString("N"));
            string app = Path.Combine(inst, "PortableApp");
            Directory.CreateDirectory(app);
            File.WriteAllText(Path.Combine(inst, "Zalo_Setup.exe"), "x"); File.WriteAllText(Path.Combine(inst, "win.iso"), "x");
            File.WriteAllText(Path.Combine(inst, "photo.jpg"), "x");
            File.WriteAllText(Path.Combine(app, "tool.exe"), "x"); File.WriteAllText(Path.Combine(app, "core.dll"), "x");
            try
            {
                if (!Installers.IsInstaller(new FileInfo(Path.Combine(inst, "Zalo_Setup.exe")), inst) ||
                    !Installers.IsInstaller(new FileInfo(Path.Combine(inst, "win.iso")), inst) ||
                    Installers.IsInstaller(new FileInfo(Path.Combine(inst, "photo.jpg")), inst) ||
                    Installers.IsInstaller(new FileInfo(Path.Combine(app, "tool.exe")), inst)) throw new Exception("Installer detection");
            }
            finally { try { Directory.Delete(inst, true); } catch { } }
            sb.Append(" installers=OK");

            // Tàn dư sau khi gỡ
            string left = Path.Combine(Fs.LocalAppData, "PCCleaner Leftover Test");
            Directory.CreateDirectory(left);
            File.WriteAllText(Path.Combine(left, "settings.ini"), "x");
            try
            {
                var fake = new InstalledApp { Name = "PCCleaner Leftover Test 2.1 (x64)", Publisher = "Nobody", Version = "2.1", Location = "", UninstallCmd = "x" };
                var found = Leftovers.Find(fake, allApps);
                if (!found.Any(f => f.Path.Equals(left, StringComparison.OrdinalIgnoreCase))) throw new Exception("Leftover not found");
                if (found.Any(f => f.Path.TrimEnd('\\').Equals(Fs.LocalAppData.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))) throw new Exception("Leftover matched a root folder");
                var generic = new InstalledApp { Name = "Microsoft", Publisher = "Microsoft", Version = "", Location = "", UninstallCmd = "x" };
                if (Leftovers.Find(generic, allApps).Count != 0) throw new Exception("Generic folder treated as leftover");
            }
            finally { try { Directory.Delete(left, true); } catch { } }
            sb.Append(" leftovers=OK");

            // Lịch sử + hiệu năng
            History.Add("rac", 1, 1234, new[] { "selftest" }, "selftest");
            LoadHistory();
            if (histList.Items.Count == 0) throw new Exception("History not loaded");
            SysMon.Cpu(); SysMon.Processes();
            MonitorTick(); MonitorTick();
            if (procList.Items.Count == 0) throw new Exception("No processes listed");
            sb.Append(" history=OK monitor=").Append(procList.Items.Count);
        }

        // ================================================================= LỊCH SỬ
        ListView histList, histItems;
        Label lblHist;

        void BuildHistoryTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 56 };
            var btnReload = Btn(UiLanguage.L("Tải lại", "Reload"), 8, 10, 120, 34, false);
            var btnOpen = Btn(UiLanguage.L("Mở thư mục nhật ký", "Open log folder"), 136, 10, 180, 34, false);
            lblHist = Lbl("", 330, 18, 830, 24);
            lblHist.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { btnReload, btnOpen, lblHist });

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
            histList = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, MultiSelect = false, HideSelection = false };
            histList.Columns.Add(UiLanguage.L("Thời gian", "Time"), 150);
            histList.Columns.Add(UiLanguage.L("Việc đã làm", "Action"), 300);
            histList.Columns.Add(UiLanguage.L("Số mục", "Items"), 80, HorizontalAlignment.Right);
            histList.Columns.Add(UiLanguage.L("Dung lượng", "Size"), 110, HorizontalAlignment.Right);
            histList.Columns.Add(UiLanguage.L("Ghi chú", "Note"), 420);
            histItems = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill };
            histItems.Columns.Add(UiLanguage.L("Chi tiết (chọn một dòng ở trên)", "Details (select a row above)"), 1100);
            split.Panel1.Controls.Add(histList);
            split.Panel2.Controls.Add(histItems);
            tab.Controls.Add(split);
            tab.Controls.Add(top);

            histList.SelectedIndexChanged += (s, e) =>
            {
                histItems.BeginUpdate();
                histItems.Items.Clear();
                if (histList.SelectedItems.Count > 0)
                    foreach (var x in ((HistoryEntry)histList.SelectedItems[0].Tag).Items) histItems.Items.Add(new ListViewItem(x));
                histItems.EndUpdate();
            };
            histItems.DoubleClick += (s, e) =>
            {
                if (histItems.SelectedItems.Count == 0) return;
                string p = histItems.SelectedItems[0].Text.Split(new[] { "  →  ", "  —  " }, StringSplitOptions.None)[0];
                if (File.Exists(p) || Directory.Exists(p)) Fs.OpenInExplorer(p);
            };
            btnReload.Click += (s, e) => LoadHistory();
            btnOpen.Click += (s, e) => { Directory.CreateDirectory(App.DataDir); System.Diagnostics.Process.Start("explorer.exe", "\"" + App.DataDir + "\""); };
            tabs.SelectedIndexChanged += (s, e) => { if (tabs.SelectedTab == tab) LoadHistory(); };
        }

        void LoadHistory()
        {
            if (histList == null || histList.IsDisposed) return;
            var all = History.Load();
            histList.BeginUpdate();
            histList.Items.Clear();
            foreach (var e in all)
            {
                DateTime t;
                var it = new ListViewItem(DateTime.TryParse(e.Time, out t) ? t.ToString("dd/MM/yyyy HH:mm") : e.Time) { Tag = e };
                it.SubItems.Add(History.KindText(e.Kind));
                it.SubItems.Add(e.Count.ToString());
                it.SubItems.Add(e.Bytes > 0 ? Fmt.Size(e.Bytes) : "");
                it.SubItems.Add(e.Note);
                histList.Items.Add(it);
            }
            histList.EndUpdate();
            histItems.Items.Clear();
            AppState.Load();
            lblHist.Text = UiLanguage.L(all.Count + " lần dọn / chuyển / gỡ đã ghi lại   •   Tổng đã giải phóng: " + Fmt.Size(AppState.TotalFreed),
                                        all.Count + " recorded actions   •   Total freed: " + Fmt.Size(AppState.TotalFreed));
        }
    }
}
