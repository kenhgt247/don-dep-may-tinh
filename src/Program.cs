// Dọn Dẹp Máy Tính — điểm khởi động và chế độ chạy ngầm.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using MessageBox = DonDep.LocalizedMessageBox;

namespace DonDep
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
            UiLanguage.Code = Settings.Load().Language;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool selfTestEn = args.Any(a => a.Equals("--selftest-en", StringComparison.OrdinalIgnoreCase));
            bool selfTest = selfTestEn || args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
            bool tray = args.Any(a => a.Equals("-Tray", StringComparison.OrdinalIgnoreCase) || a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
            bool updated = args.Any(a => a.Equals("--updated", StringComparison.OrdinalIgnoreCase));
            if (!selfTest) UpdateInstaller.CleanupOld();

            if (selfTest)
            {
                string outFile = Path.Combine(App.AppDir, "selftest.txt");
                try
                {
                    string r;
                    using (var f = new MainForm(selfTestEn ? "en" : null)) r = f.SelfTest();
                    using (var t = new TrayContext(true)) r += " TRAY_OK";
                    File.WriteAllText(outFile, r, Encoding.UTF8);
                    return 0;
                }
                catch (Exception ex)
                {
                    File.WriteAllText(outFile, "FAIL " + ex.ToString().Replace("\r", " ").Replace("\n", " / "), Encoding.UTF8);
                    return 1;
                }
            }

            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            if (tray)
            {
                bool created;
                using (var mutex = new Mutex(true, "Local\\DonDepMayTinhTray", out created))
                {
                    if (!created) return 0;
                    Application.Run(new TrayContext(false));
                }
                return 0;
            }
            if (updated)
            {
                // Vừa tự cập nhật: dọn file cũ (có thể cần chờ bản cũ thoát hẳn) và bật lại chạy ngầm nếu đang dùng.
                for (int i = 0; i < 10 && File.Exists(UpdateInstaller.OldExe); i++) { Thread.Sleep(500); UpdateInstaller.CleanupOld(); }
                if (File.Exists(Shortcut.StartupLink)) Shortcut.StartTray();
            }
            Application.Run(new MainForm(null, updated));
            return 0;
        }

        static void ReportCrash(Exception ex)
        {
            if (ex == null) return;
            App.Log("LỖI KHÔNG MONG MUỐN: " + ex);
            try { MessageBox.Show("Có lỗi xảy ra: " + ex.Message + "\n\nChi tiết đã được ghi vào nhật ký (Documents\\DonDepMayTinh).", App.Name, MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }

    /// Chạy ngầm: biểu tượng ở khay hệ thống, cảnh báo ổ sắp đầy, tự dọn rác theo lịch.
    class TrayContext : ApplicationContext
    {
        readonly NotifyIcon icon;
        readonly System.Windows.Forms.Timer timer, first;
        readonly Dictionary<string, DateTime> lastAlert = new Dictionary<string, DateTime>();
        bool busy, driveWarned;
        EventWaitHandle exitEvent;

        public TrayContext(bool test)
        {
            icon = new NotifyIcon { Icon = App.AppIcon, Text = UiLanguage.T("Dọn Dẹp Máy Tính — đang theo dõi ổ đĩa"), Visible = !test };
            var menu = new ContextMenuStrip();
            menu.Items.Add(UiLanguage.T("Mở Dọn Dẹp Máy Tính"), null, (s, e) => Shortcut.StartMain());
            menu.Items.Add(UiLanguage.T("Dọn rác hệ thống ngay"), null, (s, e) => CleanNow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(UiLanguage.T("Tắt chạy ngầm"), null, (s, e) => { icon.Visible = false; ExitThread(); });
            icon.ContextMenuStrip = menu;
            icon.DoubleClick += (s, e) => Shortcut.StartMain();
            icon.BalloonTipClicked += (s, e) => Shortcut.StartMain();
            timer = new System.Windows.Forms.Timer { Interval = 10 * 60 * 1000 };
            timer.Tick += (s, e) => Check();
            first = new System.Windows.Forms.Timer { Interval = 20 * 1000 };
            first.Tick += (s, e) => { first.Stop(); Check(); };
            if (!test)
            {
                timer.Start(); first.Start();
                // Khi app tự cập nhật, bản mới báo cho bản chạy ngầm cũ thoát ra để thay file.
                try
                {
                    exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, UpdateInstaller.TrayExitEvent);
                    var ui = SynchronizationContext.Current;
                    var waiter = new Thread(() =>
                    {
                        try { exitEvent.WaitOne(); } catch { return; }
                        if (ui != null) ui.Post(_ => { icon.Visible = false; ExitThread(); }, null);
                        else Environment.Exit(0);
                    }) { IsBackground = true };
                    waiter.Start();
                }
                catch { }
            }
        }

        void CleanNow()
        {
            if (busy) return;
            busy = true;
            icon.ShowBalloonTip(3000, UiLanguage.T(App.Name), UiLanguage.T("Đang dọn rác hệ thống..."), ToolTipIcon.Info);
            var cfg = Settings.Load();
            var t = new Thread(() =>
            {
                long f = AutoClean.Run(cfg);
                try { icon.ShowBalloonTip(8000, UiLanguage.T(App.Name), UiLanguage.T("Đã dọn xong, giải phóng khoảng " + Fmt.Size(f) + "."), ToolTipIcon.Info); } catch { }
                busy = false;
            }) { IsBackground = true };
            t.Start();
        }

        void Check()
        {
            var cfg = Settings.Load();
            if (cfg.AutoClean && !busy)
            {
                DateTime last;
                bool due = !DateTime.TryParse(cfg.LastAutoClean, out last) || (DateTime.Now - last).TotalDays >= cfg.AutoDays;
                if (due)
                {
                    busy = true;
                    var t = new Thread(() =>
                    {
                        long f = AutoClean.Run(cfg);
                        if (f > 50 * Fmt.MB) { try { icon.ShowBalloonTip(8000, UiLanguage.T(App.Name), UiLanguage.T("Đã tự dọn rác theo lịch, giải phóng khoảng " + Fmt.Size(f) + "."), ToolTipIcon.Info); } catch { } }
                        busy = false;
                    }) { IsBackground = true };
                    t.Start();
                }
            }
            if (!busy && ZaloAuto.Due(cfg))
            {
                if (!Rclone.Ready)
                {
                    if (!driveWarned)
                    {
                        driveWarned = true;
                        try { icon.ShowBalloonTip(10000, UiLanguage.T(App.Name), UiLanguage.L("Chưa tự đưa ảnh Zalo lên Drive được vì chưa kết nối Google Drive. Mở app → tab Dọn dẹp → \"Kết nối Google Drive\".",
                            "Zalo media could not be moved to Drive: Google Drive is not connected. Open the app → Cleanup tab → \"Connect Google Drive\"."), ToolTipIcon.Warning); } catch { }
                    }
                }
                else
                {
                    busy = true;
                    var t = new Thread(() =>
                    {
                        try
                        {
                            var r = ZaloAuto.Run(cfg, null);
                            string text = r.Error != null ? UiLanguage.T(r.Error)
                                : r.Files > 0 ? UiLanguage.L("Đã đưa " + r.Files + " ảnh/video/file Zalo cũ (" + Fmt.Size(r.Bytes) + ") lên Google Drive. Lấy lại bất cứ lúc nào ở tab \"Đã chuyển đi\".",
                                                             "Moved " + r.Files + " old Zalo files (" + Fmt.Size(r.Bytes) + ") to Google Drive. Restore any time from the \"Moved files\" tab.")
                                : null;
                            if (text != null) icon.ShowBalloonTip(10000, UiLanguage.T(App.Name), text, r.Error != null ? ToolTipIcon.Warning : ToolTipIcon.Info);
                        }
                        catch (Exception ex) { App.Log("Lỗi khi tự đưa ảnh Zalo lên Drive: " + ex.Message); }
                        busy = false;
                    }) { IsBackground = true };
                    t.Start();
                }
            }
            long limit = cfg.AlertGB * Fmt.GB;
            foreach (var d in Fs.FixedDrives())
            {
                try
                {
                    if (d.AvailableFreeSpace >= limit) continue;
                    DateTime prev;
                    if (lastAlert.TryGetValue(d.Name, out prev) && (DateTime.Now - prev).TotalHours < 6) continue;
                    lastAlert[d.Name] = DateTime.Now;
                    icon.ShowBalloonTip(15000, UiLanguage.T("Ổ " + d.Name.TrimEnd('\\') + " sắp đầy!"), UiLanguage.T("Chỉ còn " + Fmt.Size(d.AvailableFreeSpace) + " trống. Bấm vào đây để mở Dọn Dẹp Máy Tính."), ToolTipIcon.Warning);
                    break;
                }
                catch { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); first.Dispose(); icon.Visible = false; icon.Dispose(); try { exitEvent?.Dispose(); } catch { } }
            base.Dispose(disposing);
        }
    }
}
