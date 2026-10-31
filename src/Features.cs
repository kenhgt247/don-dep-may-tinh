// Dọn Dẹp Máy Tính — thống kê dung lượng đã giải phóng, ảnh chia sẻ, tự đưa ảnh Zalo lên Drive, tự cập nhật.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace DonDep
{
    // ------------------------------------------------------------------ trạng thái dùng chung giữa cửa sổ chính và chế độ chạy ngầm
    /// Lưu riêng trong trangthai.json (không nằm trong caidat.json) để hai tiến trình không ghi đè số liệu của nhau.
    static class AppState
    {
        public static long TotalFreed;
        public static int Cleanups;
        public static string LastZaloAuto = "", FirstUse = "";
        static string FilePath => Path.Combine(App.DataDir, "trangthai.json");

        static T WithLock<T>(Func<T> f)
        {
            using (var m = new Mutex(false, "Local\\DonDepMayTinhState"))
            {
                bool got = false;
                try { got = m.WaitOne(5000); } catch (AbandonedMutexException) { got = true; }
                try { return f(); }
                finally { if (got) try { m.ReleaseMutex(); } catch { } }
            }
        }

        public static void Load() { WithLock(() => { LoadUnlocked(); return 0; }); }

        static void LoadUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(FilePath, Encoding.UTF8));
                if (d == null) return;
                object v;
                if (d.TryGetValue("TotalFreed", out v) && v != null) TotalFreed = Convert.ToInt64(v);
                if (d.TryGetValue("Cleanups", out v) && v != null) Cleanups = Convert.ToInt32(v);
                if (d.TryGetValue("LastZaloAuto", out v) && v != null) LastZaloAuto = v.ToString();
                if (d.TryGetValue("FirstUse", out v) && v != null) FirstUse = v.ToString();
            }
            catch { }
        }

        static void SaveUnlocked()
        {
            try
            {
                Directory.CreateDirectory(App.DataDir);
                if (string.IsNullOrEmpty(FirstUse)) FirstUse = DateTime.Now.ToString("yyyy-MM-dd");
                var d = new Dictionary<string, object> { { "TotalFreed", TotalFreed }, { "Cleanups", Cleanups }, { "LastZaloAuto", LastZaloAuto }, { "FirstUse", FirstUse } };
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }

        /// Cộng thêm dung lượng vừa giải phóng (dọn rác, xóa, chuyển lên Drive…).
        public static void AddFreed(long bytes)
        {
            if (bytes <= 0) return;
            WithLock(() => { LoadUnlocked(); TotalFreed += bytes; Cleanups++; SaveUnlocked(); return 0; });
            App.Log("Tổng dung lượng app đã giúp giải phóng: " + Fmt.Size(TotalFreed));
        }

        public static void MarkZaloAuto(DateTime when)
        {
            WithLock(() => { LoadUnlocked(); LastZaloAuto = when.ToString("o"); SaveUnlocked(); return 0; });
        }

        /// Tổng dung lượng ổ trống thêm giữa hai lần đo.
        public static long Gain(Dictionary<string, long> before, Dictionary<string, long> after)
        {
            long g = 0;
            foreach (var k in after.Keys) { long b; if (before.TryGetValue(k, out b) && after[k] > b) g += after[k] - b; }
            return g;
        }
    }

    // ------------------------------------------------------------------ ảnh "khoe" lên Facebook
    static class ShareCard
    {
        static Image Logo()
        {
            // Lấy ảnh PNG 160 px nằm trong file biểu tượng của app.
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("DonDep.ico"))
                {
                    if (s != null)
                    {
                        var buf = new byte[s.Length]; s.Read(buf, 0, buf.Length);
                        int count = BitConverter.ToUInt16(buf, 4);
                        int best = -1, bestW = 0;
                        for (int i = 0; i < count; i++)
                        {
                            int w = buf[6 + 16 * i]; if (w == 0) w = 256;
                            if (w > bestW) { bestW = w; best = i; }
                        }
                        if (best >= 0)
                        {
                            int size = BitConverter.ToInt32(buf, 6 + 16 * best + 8), off = BitConverter.ToInt32(buf, 6 + 16 * best + 12);
                            using (var ms = new MemoryStream(buf, off, size))
                            {
                                try { return new Bitmap(Image.FromStream(ms)); }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
            return App.AppIcon.ToBitmap();
        }

        /// Tạo ảnh 1200×630 (đúng khổ ảnh Facebook). Trả về đường dẫn file PNG.
        public static string Create(long freed, int cleanups, string folder = null)
        {
            bool en = UiLanguage.English;
            folder = folder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "PC Cleaner");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "PC-Cleaner-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            using (var bmp = new Bitmap(1200, 630))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, 1200, 630), Color.FromArgb(11, 155, 255), Color.FromArgb(24, 66, 189), 35f))
                    g.FillRectangle(bg, 0, 0, 1200, 630);
                using (var wave = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    g.FillEllipse(wave, 760, -220, 640, 640);
                    g.FillEllipse(wave, -200, 430, 700, 420);
                }
                using (var logo = Logo()) g.DrawImage(logo, new Rectangle(80, 70, 120, 120));
                var white = Brushes.White;
                using (var light = new SolidBrush(Color.FromArgb(220, 235, 255)))
                using (var mint = new SolidBrush(Color.FromArgb(158, 255, 235)))
                using (var fName = new Font("Segoe UI", 34f, FontStyle.Bold))
                using (var fSmall = new Font("Segoe UI", 22f))
                using (var fLead = new Font("Segoe UI", 30f))
                using (var fBig = new Font("Segoe UI", 96f, FontStyle.Bold))
                using (var fFoot = new Font("Segoe UI", 20f))
                {
                    g.DrawString("PC CLEANER", fName, white, 222, 82);
                    g.DrawString(en ? "Free disk cleanup for Windows" : "Dọn Dẹp Máy Tính — miễn phí", fSmall, light, 226, 140);
                    g.DrawString(en ? "My PC is lighter by" : "Máy tính của mình vừa nhẹ đi", fLead, white, 80, 240);
                    g.DrawString(Fmt.Size(freed), fBig, mint, 66, 290);
                    string sub = en ? "freed across " + cleanups + " cleanups — no ads, no VIP fees."
                                    : "sau " + cleanups + " lần dọn — không quảng cáo, không thu phí.";
                    g.DrawString(sub, fSmall, white, 84, 452);
                    g.DrawString((en ? "Download free: " : "Tải miễn phí: ") + "github.com/" + App.Repo, fFoot, light, 84, 540);
                }
                bmp.Save(file, ImageFormat.Png);
            }
            return file;
        }
    }

    // ------------------------------------------------------------------ chuyển file lên Google Drive (dùng chung)
    static class DriveTransfer
    {
        public static List<MovedEntry> Move(List<CleanItem> items, Func<CleanItem, string> category, Action<string> progress)
        {
            var moved = new List<MovedEntry>();
            foreach (var g in items.GroupBy(o => o.Root, StringComparer.OrdinalIgnoreCase))
            {
                string root = g.Key;
                string remotePath = "DonDep/" + App.PcName + "/" + Fs.SafeName(root);
                string remote = App.RemoteName + ":" + remotePath;
                var rel = g.Select(o => Fs.RelPath(root, o.Path).Replace('\\', '/')).ToList();
                App.Log("Tải lên Google Drive: " + rel.Count + " file từ " + root);
                Rclone.Move(root, rel, remote, progress);
                foreach (var o in g)
                {
                    if (File.Exists(o.Path)) continue;   // còn trên máy = chưa tải lên được, giữ nguyên
                    string r = Fs.RelPath(root, o.Path).Replace('\\', '/');
                    moved.Add(new MovedEntry
                    {
                        Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Category = category(o), Orig = o.Path,
                        Dest = "Google Drive/" + remotePath + "/" + r, SizeText = Fmt.Size(o.Size), Code = remote + "/" + r, Bytes = o.Size
                    });
                }
            }
            return moved;
        }
    }

    // ------------------------------------------------------------------ tự đưa ảnh / video / file Zalo cũ lên Google Drive
    static class ZaloAuto
    {
        public static readonly string[] Skip = { "sticker*", "emoji*", "avatar*", "*cache*", "thumb*", "*Thumb", "zinstant", "fileNoise" };

        public static List<CleanItem> FindOld(Settings cfg, int days, Action<string> progress)
        {
            var before = DateTime.Now.AddDays(-Math.Max(7, days));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var r = new List<CleanItem>();
            foreach (var z in Junk.ZaloRoots(cfg))
            {
                if (!Directory.Exists(z.Path)) continue;
                foreach (var f in Fs.Walk(z.Path, Skip, 0, before, z.Ext, progress))
                    if (seen.Add(f.FullName))
                        r.Add(new CleanItem { Category = "zalo", Kind = "chuyen", Path = f.FullName, Root = z.Path, Size = f.Length, Name = f.Name, Folder = f.DirectoryName, Date = f.LastWriteTime });
            }
            return r;
        }

        public static bool Due(Settings cfg)
        {
            if (!cfg.ZaloAuto) return false;
            AppState.Load();
            DateTime last;
            return !DateTime.TryParse(AppState.LastZaloAuto, out last) || (DateTime.Now - last).TotalDays >= Math.Max(1, cfg.ZaloAutoEvery);
        }

        public class Result { public int Files, Found; public long Bytes; public string Error; }

        /// Chạy một lượt (dùng ở chế độ chạy ngầm). Không bao giờ xóa file: rclone chỉ bỏ bản trên máy sau khi đã tải lên và kiểm tra xong.
        public static Result Run(Settings cfg, Action<string> progress)
        {
            var res = new Result();
            if (!Rclone.Ready) { res.Error = "Chưa kết nối Google Drive. Mở app → tab Dọn dẹp → bấm \"Kết nối Google Drive\"."; return res; }
            var items = FindOld(cfg, cfg.ZaloAutoDays, progress);
            res.Found = items.Count;
            AppState.MarkZaloAuto(DateTime.Now);
            if (items.Count == 0) { App.Log("Tự đưa ảnh Zalo lên Drive: không có file nào cũ hơn " + cfg.ZaloAutoDays + " ngày."); return res; }
            long need = items.Sum(o => o.Size);
            double free = Rclone.Free();
            if (free >= 0 && free - need < 500 * Fmt.MB)
            {
                res.Error = "Google Drive không đủ chỗ (còn " + Fmt.Size(free) + ", cần " + Fmt.Size(need) + ").";
                App.Log("Tự đưa ảnh Zalo lên Drive: " + res.Error);
                return res;
            }
            App.Log("===== Tự đưa " + items.Count + " file Zalo cũ (" + Fmt.Size(need) + ") lên Google Drive =====");
            var moved = DriveTransfer.Move(items, o => "Zalo", progress);
            if (moved.Count > 0)
            {
                App.Log("Danh sách file đã chuyển: " + MovedStore.Save(moved));
                res.Files = moved.Count;
                res.Bytes = moved.Sum(m => m.Bytes);
                AppState.AddFreed(res.Bytes);
            }
            App.Log("Đã đưa " + moved.Count + "/" + items.Count + " file Zalo lên Google Drive.");
            return res;
        }
    }

    // ------------------------------------------------------------------ tự cập nhật
    static class UpdateInstaller
    {
        public const string TrayExitEvent = "Local\\DonDepMayTinhTrayExit";
        public static string OldExe => App.ExePath + ".old";

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        /// Xóa file .old còn sót lại từ lần cập nhật trước.
        public static void CleanupOld()
        {
            try { if (File.Exists(OldExe)) File.Delete(OldExe); } catch { }
        }

        /// Tải bản mới, kiểm tra, thay file .exe đang chạy. Trả về null nếu thành công, ngược lại là lý do thất bại.
        public static string Install(Updater.Info info, Action<string> progress)
        {
            if (info == null || string.IsNullOrEmpty(info.ZipUrl)) return "Bản phát hành chưa có file tải về.";
            string work = Path.Combine(Path.GetTempPath(), "DonDepUpdate_" + info.Latest);
            try
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
                Directory.CreateDirectory(work);
                string zip = Path.Combine(work, "update.zip");
                progress?.Invoke("Đang tải bản " + info.Latest + "...");
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "DonDepMayTinh/" + App.Version);
                    wc.DownloadFile(info.ZipUrl, zip);
                }
                long len = new FileInfo(zip).Length;
                if (info.ZipSize > 0 && len != info.ZipSize) return "File tải về không đủ dung lượng (kiểm tra kết nối mạng rồi thử lại).";
                if (!string.IsNullOrEmpty(info.Sha256) && !string.Equals(Sha256File(zip), info.Sha256, StringComparison.OrdinalIgnoreCase))
                    return "File tải về không khớp mã kiểm tra của GitHub — đã hủy để an toàn.";
                string dir = Path.Combine(work, "x");
                ZipFile.ExtractToDirectory(zip, dir);
                string newExe = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories)
                    .OrderByDescending(f => Path.GetFileName(f).Equals("Don Dep May Tinh.exe", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (newExe == null) return "Không tìm thấy file chương trình trong bản tải về.";
                if (AssemblyName.GetAssemblyName(newExe).Name != "DonDepMayTinh") return "File trong bản tải về không phải Dọn Dẹp Máy Tính — đã hủy.";
                Version nv;
                if (!Version.TryParse(FileVersionInfo.GetVersionInfo(newExe).FileVersion ?? "", out nv) || nv < info.Latest)
                    return "Bản tải về không đúng phiên bản " + info.Latest + ".";

                // Báo cho bản đang chạy ngầm (nếu có) tự thoát để thay file.
                try
                {
                    EventWaitHandle ev;
                    if (EventWaitHandle.TryOpenExisting(TrayExitEvent, out ev)) using (ev) { ev.Set(); Thread.Sleep(1500); }
                }
                catch { }

                progress?.Invoke("Đang cài bản " + info.Latest + "...");
                string cur = App.ExePath;
                CleanupOld();
                File.Move(cur, OldExe);   // Windows cho phép đổi tên file .exe đang chạy
                try { File.Copy(newExe, cur, false); }
                catch
                {
                    try { if (!File.Exists(cur) && File.Exists(OldExe)) File.Move(OldExe, cur); } catch { }
                    throw;
                }
                // Các file đi kèm (cấu hình, hướng dẫn). Không quan trọng nếu lỗi.
                string srcDir = Path.GetDirectoryName(newExe);
                foreach (var f in Directory.GetFiles(srcDir))
                {
                    if (f.Equals(newExe, StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Path.GetFileName(f);
                    if (name.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase)) name = Path.GetFileName(cur) + ".config";
                    try { File.Copy(f, Path.Combine(App.AppDir, name), true); } catch { }
                }
                App.Log("Đã cập nhật lên bản " + info.Latest + ".");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return "App đang nằm trong thư mục cần quyền quản trị nên không tự thay file được.";
            }
            catch (Exception ex)
            {
                App.Log("Lỗi cập nhật: " + ex);
                return "Không cập nhật được: " + ex.Message;
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { }
            }
        }
    }
}
