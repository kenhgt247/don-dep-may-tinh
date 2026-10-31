// Dọn Dẹp Máy Tính — thống kê, tự đưa ảnh Zalo lên Drive, tự cập nhật (phần giao diện).
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DonDep
{
    partial class MainForm
    {
        System.Windows.Forms.Panel settingsContent;

        /// Chụp ảnh từng tab (chỉ dùng khi kiểm tra tự động trên GitHub).
        public void SaveScreens(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            string lang = UiLanguage.Code;
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(0, 0);
            Show();
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                tabs.SelectedIndex = i;
                Application.DoEvents();
                using (var bmp = new System.Drawing.Bitmap(Width, Height))
                {
                    DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, Width, Height));
                    bmp.Save(System.IO.Path.Combine(dir, lang + "_tab" + i + ".png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            if (settingsContent != null)
                using (var bmp = new System.Drawing.Bitmap(settingsContent.Width, settingsContent.Height))
                {
                    settingsContent.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(System.IO.Path.Combine(dir, lang + "_settings_full.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            ShareCard.Create(37L * Fmt.GB + 512 * Fmt.MB, 12, dir);
            Hide();
        }
        void UpdateStatsLabel()
        {
            if (lblStats == null || lblStats.IsDisposed) return;
            AppState.Load();
            lblStats.Text = AppState.TotalFreed > 0
                ? UiLanguage.L("App đã giúp bạn giải phóng tổng cộng " + Fmt.Size(AppState.TotalFreed) + " qua " + AppState.Cleanups + " lần dọn.",
                               "The app has freed " + Fmt.Size(AppState.TotalFreed) + " in total across " + AppState.Cleanups + " cleanups.")
                : UiLanguage.L("Chưa có số liệu — dọn máy lần đầu để bắt đầu tính.", "No results yet — clean your PC once to start counting.");
        }

        void UpdateZaloLastLabel()
        {
            if (lblZaloLast == null || lblZaloLast.IsDisposed) return;
            AppState.Load();
            DateTime t;
            string last = DateTime.TryParse(AppState.LastZaloAuto, out t) ? t.ToString("dd/MM/yyyy HH:mm") : UiLanguage.L("chưa lần nào", "never");
            string drive = Rclone.Ready ? UiLanguage.L("Google Drive: đã kết nối", "Google Drive: connected") : UiLanguage.L("Google Drive: CHƯA kết nối (tab Dọn dẹp → Kết nối Google Drive)", "Google Drive: NOT connected (Cleanup tab → Connect Google Drive)");
            lblZaloLast.Text = UiLanguage.L("Lần chạy gần nhất: ", "Last run: ") + last + "   •   " + drive;
        }

        async Task RunZaloAutoNowAsync()
        {
            if (!Rclone.Ready) { Msg(UiLanguage.L("Hãy kết nối Google Drive trước: tab Dọn dẹp → \"Kết nối Google Drive\".", "Connect Google Drive first: Cleanup tab → \"Connect Google Drive\"."), MessageBoxIcon.Warning); return; }
            int days = (int)numZaloDays.Value;
            SetBusy(true);
            Status(UiLanguage.L("Đang tìm ảnh, video, file Zalo cũ...", "Looking for old Zalo files..."));
            var items = await Task.Run(() => ZaloAuto.FindOld(cfg, days, status.Report));
            if (items.Count == 0)
            {
                Status(UiLanguage.L("Sẵn sàng.", "Ready."));
                Msg(UiLanguage.L("Không có ảnh, video, file Zalo nào cũ hơn " + days + " ngày.", "No Zalo files older than " + days + " days."));
                return;
            }
            long bytes = items.Sum(o => o.Size);
            Status(UiLanguage.L("Đang kiểm tra dung lượng Google Drive...", "Checking Google Drive space..."));
            double free = await Task.Run(() => Rclone.Free());
            if (free >= 0 && free - bytes < 500 * Fmt.MB)
            {
                Msg(UiLanguage.L("Google Drive không đủ chỗ.\n\nCòn trống: " + Fmt.Size(free) + "\nCần: " + Fmt.Size(bytes),
                                 "Not enough space on Google Drive.\n\nFree: " + Fmt.Size(free) + "\nNeeded: " + Fmt.Size(bytes)), MessageBoxIcon.Warning);
                return;
            }
            string q = UiLanguage.L(
                "Sẽ đưa " + items.Count + " ảnh/video/file Zalo cũ hơn " + days + " ngày (" + Fmt.Size(bytes) + ") lên Google Drive" + (free >= 0 ? " (Drive còn trống " + Fmt.Size(free) + ")" : "") + ".\n\n" +
                "• File chỉ bị bỏ khỏi máy sau khi đã tải lên và kiểm tra xong.\n• Ảnh/file đã chuyển sẽ không mở được trong Zalo trên máy này cho đến khi bạn lấy lại (tab \"Đã chuyển đi\").\n• Tin nhắn Zalo không bị đụng tới.\n\nTiếp tục?",
                "Move " + items.Count + " Zalo files older than " + days + " days (" + Fmt.Size(bytes) + ") to Google Drive" + (free >= 0 ? " (Drive free: " + Fmt.Size(free) + ")" : "") + "?\n\n" +
                "• Files are removed from the PC only after upload and verification.\n• Moved files won't open inside Zalo on this PC until restored (\"Moved files\" tab).\n• Zalo messages are never touched.\n\nContinue?");
            if (!Confirm(UiLanguage.P(q))) { Status(UiLanguage.L("Đã hủy.", "Cancelled.")); return; }
            var moved = await Task.Run(() =>
            {
                var m = DriveTransfer.Move(items, o => "Zalo", status.Report);
                if (m.Count > 0) App.Log("Danh sách file đã chuyển: " + MovedStore.Save(m));
                return m;
            });
            long done = moved.Sum(m => m.Bytes);
            AppState.AddFreed(done);
            AppState.MarkZaloAuto(DateTime.Now);
            UpdateZaloLastLabel();
            UpdateStatsLabel();
            UpdateDrives();
            Status(UiLanguage.L("Đã xong.", "Done."));
            Msg(UiLanguage.P(UiLanguage.L("Đã đưa " + moved.Count + "/" + items.Count + " file (" + Fmt.Size(done) + ") lên Google Drive." + (moved.Count < items.Count ? "\nFile nào chưa tải lên được vẫn còn nguyên trên máy (xem nhật ký)." : ""),
                                          "Moved " + moved.Count + "/" + items.Count + " files (" + Fmt.Size(done) + ") to Google Drive." + (moved.Count < items.Count ? "\nFiles that could not be uploaded remain on the PC (see log)." : ""))));
        }

        async Task CheckUpdate(bool manual)
        {
            if (manual) Status("Đang kiểm tra bản mới...");
            var info = await Updater.CheckAsync();
            if (info == null)
            {
                if (manual) Msg("Không kiểm tra được bản mới (máy chưa có mạng hoặc chưa có bản phát hành nào).", MessageBoxIcon.Warning);
            }
            else if (Updater.IsNewer(info))
            {
                App.Log("Có bản mới " + info.Latest + " (bạn đang dùng " + App.Version + ").");
                if (string.IsNullOrEmpty(info.ZipUrl))
                {
                    if (Confirm("Đã có bản mới " + info.Latest + " (bạn đang dùng " + App.Version + ").\n\nMở trang tải về?")) { try { Process.Start(info.Url); } catch { } }
                }
                else if (Confirm(UiLanguage.P(UiLanguage.L(
                    "Đã có bản mới " + info.Latest + " (bạn đang dùng " + App.Version + ").\n\nCập nhật ngay? App sẽ tự tải bản mới (" + Fmt.Size(info.ZipSize) + "), kiểm tra, thay thế rồi mở lại. Thiết lập và dữ liệu của bạn giữ nguyên.",
                    "Version " + info.Latest + " is available (you have " + App.Version + ").\n\nUpdate now? The app will download it (" + Fmt.Size(info.ZipSize) + "), verify it, replace itself and restart. Your settings and data are kept."))))
                {
                    if (isBusy) { Msg(UiLanguage.L("App đang bận, hãy cập nhật sau khi xong việc (tab Cài đặt → Kiểm tra bản mới).", "The app is busy. Update when it's done (Settings → Check for updates).")); return; }
                    SetBusy(true);
                    string err = await Task.Run(() => UpdateInstaller.Install(info, status.Report));
                    if (err == null)
                    {
                        try { Process.Start(new ProcessStartInfo(App.ExePath, "--updated") { UseShellExecute = false }); } catch { }
                        SetBusy(false);
                        Close();
                        return;
                    }
                    SetBusy(false);
                    Status(UiLanguage.L("Sẵn sàng.", "Ready."));
                    if (Confirm(UiLanguage.T(err) + UiLanguage.L("\n\nMở trang tải về để cập nhật thủ công?", "\n\nOpen the download page to update manually?"))) { try { Process.Start(info.Url); } catch { } }
                }
            }
            else if (manual) Msg("Bạn đang dùng bản mới nhất (" + App.Version + ").");
            if (manual) Status("Sẵn sàng.");
        }
    }
}
