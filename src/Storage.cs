// Dọn Dẹp Máy Tính — Google Drive (rclone), danh sách file đã chuyển, cập nhật, lối tắt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DonDep
{
    static class Rclone
    {
        static string _home;
        /// Công cụ rclone (~70 MB) đặt ở ổ còn nhiều chỗ nhất nếu ổ C quá đầy.
        public static string Home
        {
            get
            {
                if (_home != null) return _home;
                string docs = App.DataDir;
                if (File.Exists(Path.Combine(docs, @"rclone\rclone.exe"))) return _home = docs;
                string best = null; long bestFree = 0;
                foreach (var d in Fs.FixedDrives())
                {
                    string cand = Path.Combine(d.RootDirectory.FullName, "DonDepMayTinh");
                    if (File.Exists(Path.Combine(cand, @"rclone\rclone.exe"))) return _home = cand;
                    long free = 0;
                    try { free = d.AvailableFreeSpace; } catch { }
                    if (free > bestFree) { bestFree = free; best = cand; }
                }
                long docsFree = Fs.DriveFree(docs);
                _home = (docsFree > 300 * Fmt.MB || best == null) ? docs : best;
                return _home;
            }
        }
        public static string Exe => Path.Combine(Home, @"rclone\rclone.exe");
        public static string Conf => Path.Combine(Home, "rclone.conf");
        public static string ConfArg => " --config " + Q(Conf);

        public static bool Configured
        {
            get
            {
                try { return File.Exists(Conf) && Regex.IsMatch(File.ReadAllText(Conf), @"(?m)^\[" + App.RemoteName + @"\]"); }
                catch { return false; }
            }
        }
        public static bool Ready => Configured && File.Exists(Exe);

        public static string Q(string s)
        {
            if (s.EndsWith("\\")) s += ".";
            return "\"" + s + "\"";
        }

        public class RunResult { public int Code; public string Out = "", Err = ""; }

        public static RunResult Run(string args, int timeoutSec = 0, string tailFile = null, Action<string> progress = null)
        {
            var psi = new ProcessStartInfo(Exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            var res = new RunResult();
            using (var p = Process.Start(psi))
            {
                var tOut = p.StandardOutput.ReadToEndAsync();
                var tErr = p.StandardError.ReadToEndAsync();
                var sw = Stopwatch.StartNew();
                while (!p.WaitForExit(1000))
                {
                    if (tailFile != null && progress != null)
                    {
                        string line = LastLine(tailFile);
                        if (line.Length > 0) progress(Regex.Replace(line, @"^.*?(INFO|NOTICE)\s*:\s*", ""));
                    }
                    if (timeoutSec > 0 && sw.Elapsed.TotalSeconds > timeoutSec) { try { p.Kill(); } catch { } break; }
                }
                p.WaitForExit();
                res.Code = p.ExitCode;
                res.Out = tOut.Result; res.Err = tErr.Result;
            }
            return res;
        }

        static string LastLine(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, fs.Length - 600);
                    fs.Seek(start, SeekOrigin.Begin);
                    var buf = new byte[fs.Length - start];
                    fs.Read(buf, 0, buf.Length);
                    var lines = Encoding.UTF8.GetString(buf).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return lines.Length > 0 ? lines[lines.Length - 1] : "";
                }
            }
            catch { return ""; }
        }

        /// Tải rclone từ trang chính thức. Trả về thông báo lỗi (null nếu thành công).
        public static string Install(Action<string> progress)
        {
            if (File.Exists(Exe)) return null;
            Directory.CreateDirectory(Home);
            long free = Fs.DriveFree(Home);
            if (free < 150 * Fmt.MB)
                return "Không đủ chỗ trống để tải công cụ kết nối Google Drive (cần khoảng 150 MB, ổ " + Fs.Root(Home) + " chỉ còn " + Fmt.Size(free) + ").\n\nHãy dọn Rác hệ thống trước để giải phóng chỗ, sau đó kết nối lại.";
            string arch = Environment.Is64BitOperatingSystem ? "amd64" : "386";
            if (string.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"), "ARM64", StringComparison.OrdinalIgnoreCase)) arch = "arm64";
            string url = "https://downloads.rclone.org/rclone-current-windows-" + arch + ".zip";
            string zip = Path.Combine(Home, "rclone_tai_ve.zip");
            string tmp = Path.Combine(Home, "rclone_giai_nen");
            try
            {
                App.Log("Đang tải rclone từ " + url);
                progress?.Invoke("Đang tải rclone...");
                using (var wc = new WebClient()) wc.DownloadFile(url, zip);
                if (!File.Exists(zip) || new FileInfo(zip).Length < Fmt.MB) return "Tải về không thành công (kiểm tra kết nối mạng).";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                ZipFile.ExtractToDirectory(zip, tmp);
                var exe = Directory.GetFiles(tmp, "rclone.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (exe == null) return "Không tìm thấy rclone.exe trong file tải về.";
                Directory.CreateDirectory(Path.GetDirectoryName(Exe));
                File.Copy(exe, Exe, true);
                App.Log("Đã cài rclone vào " + Path.GetDirectoryName(Exe));
                return null;
            }
            catch (Exception ex)
            {
                App.Log("Lỗi khi tải rclone: " + ex.Message);
                return "Không tải được rclone: " + ex.Message;
            }
            finally
            {
                try { if (File.Exists(zip)) File.Delete(zip); } catch { }
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { }
            }
        }

        public static bool Connect()
        {
            if (Configured) Run("config delete " + App.RemoteName + ConfArg, 30);
            App.Log("Mở trình duyệt để đăng nhập Google Drive...");
            var r = Run("config create " + App.RemoteName + " drive scope=drive.file" + ConfArg, 600);
            bool ok = r.Code == 0 && Configured;
            App.Log(ok ? "Đã kết nối Google Drive." : "Kết nối Google Drive thất bại: " + r.Err);
            return ok;
        }

        /// Dung lượng còn trống trên Google Drive (byte), hoặc -1 nếu không đọc được.
        public static double Free()
        {
            if (!File.Exists(Exe)) return -1;
            var r = Run("about " + App.RemoteName + ": --json" + ConfArg, 60);
            if (r.Code != 0) return -1;
            try
            {
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(r.Out);
                object v;
                if (d != null && d.TryGetValue("free", out v) && v != null) return Convert.ToDouble(v);
            }
            catch { }
            return -1;
        }

        /// Chuyển các file (đường dẫn tương đối so với root) lên Drive. rclone chỉ xóa file gốc sau khi tải lên và kiểm tra xong.
        public static RunResult Move(string root, List<string> rel, string remote, Action<string> progress)
        {
            string listFile = Path.Combine(App.DataDir, "danh_sach_tam.txt");
            File.WriteAllLines(listFile, rel.ToArray(), new UTF8Encoding(false));
            string rlog = Path.Combine(App.DataDir, "rclone_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            string args = "move " + Q(root) + " " + Q(remote) + " --files-from-raw " + Q(listFile) + ConfArg +
                          " --log-file " + Q(rlog) + " --log-level INFO --stats 2s --stats-one-line --transfers 4 --retries 3 --low-level-retries 10";
            var r = Run(args, 0, rlog, s => progress?.Invoke("Đang tải lên: " + s));
            if (r.Code != 0) App.Log("rclone báo lỗi (mã " + r.Code + "). Chi tiết trong: " + rlog);
            try { File.Delete(listFile); } catch { }
            return r;
        }

        public static RunResult CopyTo(string remotePath, string localPath)
        {
            return Run("copyto " + Q(remotePath) + " " + Q(localPath) + ConfArg + " --retries 3");
        }
    }

    // ------------------------------------------------------------------ danh sách file đã chuyển (CSV giống bản cũ)
    class MovedEntry
    {
        public string Time = "", Category = "", Orig = "", Dest = "", SizeText = "", Code = "";
        public long Bytes;
        public string State = "";
    }

    static class MovedStore
    {
        static readonly string[] Header = { "Thời gian", "Loại", "Đường dẫn gốc", "Nơi lưu mới", "Dung lượng", "Mã nơi lưu", "Số byte" };

        public static string Save(List<MovedEntry> list)
        {
            string csv = Path.Combine(App.DataDir, "danh_sach_da_chuyen_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", Header.Select(Quote)));
            foreach (var e in list)
                sb.AppendLine(string.Join(",", new[] { e.Time, e.Category, e.Orig, e.Dest, e.SizeText, e.Code, e.Bytes.ToString() }.Select(Quote)));
            Directory.CreateDirectory(App.DataDir);
            File.WriteAllText(csv, sb.ToString(), new UTF8Encoding(true));
            return csv;
        }

        static string Quote(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }

        public static HashSet<string> Restored()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(App.RestoredFile)) foreach (var l in File.ReadAllLines(App.RestoredFile, Encoding.UTF8)) if (l.Length > 0) set.Add(l.Trim('﻿')); } catch { }
            return set;
        }

        public static void MarkRestored(MovedEntry e)
        {
            try { File.AppendAllText(App.RestoredFile, e.Orig + "|" + e.Code + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        public static List<MovedEntry> LoadAll()
        {
            var restored = Restored();
            var result = new List<MovedEntry>();
            if (!Directory.Exists(App.DataDir)) return result;
            foreach (var file in Directory.GetFiles(App.DataDir, "danh_sach_da_chuyen_*.csv").OrderByDescending(f => f))
            {
                List<string[]> rows;
                try { rows = ParseCsv(File.ReadAllText(file, Encoding.UTF8)); } catch { continue; }
                if (rows.Count < 2) continue;
                var head = rows[0].Select(h => h.Trim('﻿', ' ')).ToList();
                Func<string[], string, string> col = (r, name) => { int i = head.IndexOf(name); return i >= 0 && i < r.Length ? r[i] : ""; };
                foreach (var r in rows.Skip(1))
                {
                    var e = new MovedEntry
                    {
                        Time = col(r, "Thời gian"), Category = col(r, "Loại"), Orig = col(r, "Đường dẫn gốc"),
                        Dest = col(r, "Nơi lưu mới"), SizeText = col(r, "Dung lượng"), Code = col(r, "Mã nơi lưu")
                    };
                    if (string.IsNullOrEmpty(e.Orig)) continue;
                    long b; if (long.TryParse(col(r, "Số byte"), out b)) e.Bytes = b;
                    if (string.IsNullOrEmpty(e.Code))
                        e.Code = e.Dest.StartsWith("Google Drive/") ? App.RemoteName + ":" + e.Dest.Substring(13) : e.Dest;
                    if (restored.Contains(e.Orig + "|" + e.Code)) e.State = "Đã lấy về";
                    else if (File.Exists(e.Orig)) e.State = "Đã có trên máy";
                    else e.State = "Đang ở nơi lưu";
                    result.Add(e);
                }
            }
            return result;
        }

        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cur = new StringBuilder();
            bool inQ = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cur.Append('"'); i++; }
                        else inQ = false;
                    }
                    else cur.Append(c);
                }
                else if (c == '"') inQ = true;
                else if (c == ',') { row.Add(cur.ToString()); cur.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cur.ToString()); cur.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                    row = new List<string>();
                }
                else cur.Append(c);
            }
            if (cur.Length > 0 || row.Count > 0) { row.Add(cur.ToString()); rows.Add(row.ToArray()); }
            return rows;
        }
    }

    // ------------------------------------------------------------------ kiểm tra bản mới
    static class Updater
    {
        public class Info { public Version Latest; public string Url, ZipUrl, Sha256; public long ZipSize; }

        public static Task<Info> CheckAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var wc = new WebClient())
                    {
                        wc.Headers.Add("User-Agent", "DonDepMayTinh/" + App.Version);
                        wc.Encoding = Encoding.UTF8;
                        string json = wc.DownloadString("https://api.github.com/repos/" + App.Repo + "/releases/latest");
                        var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        Version v;
                        string tag = (d["tag_name"] ?? "").ToString().TrimStart('v', 'V');
                        if (!Version.TryParse(tag, out v)) return null;
                        var info = new Info { Latest = v, Url = (d["html_url"] ?? "").ToString() };
                        object assets;
                        if (d.TryGetValue("assets", out assets) && assets is System.Collections.ArrayList)
                            foreach (var a in (System.Collections.ArrayList)assets)
                            {
                                var ad = a as Dictionary<string, object>;
                                if (ad == null || !(ad["name"] ?? "").ToString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                                info.ZipUrl = (ad["browser_download_url"] ?? "").ToString();
                                object sz, dg;
                                if (ad.TryGetValue("size", out sz) && sz != null) info.ZipSize = Convert.ToInt64(sz);
                                if (ad.TryGetValue("digest", out dg) && dg != null && dg.ToString().StartsWith("sha256:")) info.Sha256 = dg.ToString().Substring(7);
                                break;
                            }
                        return info;
                    }
                }
                catch { return null; }
            });
        }

        public static bool IsNewer(Info i) { return i != null && i.Latest > Version.Parse(App.Version); }
    }

    // ------------------------------------------------------------------ lối tắt, chạy cùng Windows
    static class Shortcut
    {
        public static string StartupLink => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Don Dep May Tinh (chay ngam).lnk");

        public static void Create(string lnk, string args, string desc)
        {
            // Native Windows Shell Link API handles Unicode paths, OneDrive-redirection
            // and Start Menu locations without WScript.Shell late-binding failures.
            NativeShortcut.Create(lnk, App.ExePath, args, desc);
        }

        public static void SetStartup(bool on)
        {
            if (on) Create(StartupLink, "-Tray", "Dọn Dẹp Máy Tính - cảnh báo khi ổ đĩa sắp đầy");
            else if (File.Exists(StartupLink)) { try { File.Delete(StartupLink); } catch { } }
        }

        public static void StartTray() { try { Process.Start(App.ExePath, "-Tray"); } catch { } }
        public static void StartMain() { try { Process.Start(App.ExePath); } catch { } }
    }

    // ------------------------------------------------------------------ tự dọn rác (dùng khi chạy ngầm)
    static class AutoClean
    {
        public static long Run(Settings cfg)
        {
            bool zalo = Junk.ZaloRunning();
            var before = Fs.FreeMap();
            foreach (var j in Junk.Sources(cfg))
            {
                if (zalo && j.Name.Contains("Zalo")) continue;
                if (!j.Default) continue;                       // bản Zalo / CapCut cũ: chỉ dọn khi người dùng tự chọn
                if (JunkSource.AnyRunning(j.Procs)) continue;   // phần mềm đang mở: để lần sau
                Junk.Remove(j.Path, j.Name, j.Before);
            }
            var after = Fs.FreeMap();
            long freed = AppState.Gain(before, after);
            AppState.AddFreed(freed);
            History.Add("tudon", 1, freed);
            cfg.LastAutoClean = DateTime.Now.ToString("o");
            cfg.Save();
            App.Log("Tự dọn rác: đã giải phóng khoảng " + Fmt.Size(freed));
            return freed;
        }
    }
}
