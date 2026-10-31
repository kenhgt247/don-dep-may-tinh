// Dọn Dẹp Máy Tính — danh sách bảo vệ, lịch sử dọn dẹp, tàn dư sau khi gỡ, file cài đặt thừa, theo dõi hiệu năng.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace DonDep
{
    // ------------------------------------------------------------------ danh sách bảo vệ: không bao giờ xóa / chuyển
    static class Protect
    {
        static List<string> list;
        static readonly object lk = new object();

        public static void Invalidate() { lock (lk) list = null; }

        static List<string> Items
        {
            get
            {
                lock (lk)
                {
                    if (list == null)
                        list = Settings.Load().Protected.Select(Norm).Where(p => p.Length > 2).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    return list;
                }
            }
        }

        static string Norm(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\'); } catch { return (p ?? "").TrimEnd('\\'); }
        }

        /// Đường dẫn này nằm trong (hoặc chính là) một mục được bảo vệ.
        public static bool Has(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var items = Items;
            if (items.Count == 0) return false;
            string p = Norm(path);
            foreach (var x in items)
                if (p.Equals(x, StringComparison.OrdinalIgnoreCase) || p.StartsWith(x + "\\", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// Thư mục này chứa bên trong một mục được bảo vệ (xóa cả thư mục sẽ mất mục đó).
        public static bool Contains(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            string d = Norm(dir) + "\\";
            return Items.Any(x => x.StartsWith(d, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Blocks(string path) { return Has(path) || Contains(path); }
    }

    // ------------------------------------------------------------------ lịch sử dọn dẹp
    class HistoryEntry
    {
        public string Time = "", Kind = "", Note = "";
        public int Count;
        public long Bytes;
        public List<string> Items = new List<string>();
    }

    static class History
    {
        static string FilePath => Path.Combine(App.DataDir, "lich_su.jsonl");
        static readonly object lk = new object();

        public static readonly string[] Kinds = { "rac", "xoa", "thungrac", "drive", "ongoai", "trung", "trinhduyet", "smart", "smart_hoantac", "go", "tandu", "tudon", "zalo_tudong", "caidat", "laylai" };

        public static string KindText(string k)
        {
            switch (k)
            {
                case "rac": return UiLanguage.L("Dọn rác hệ thống", "System junk cleaned");
                case "xoa": return UiLanguage.L("Xóa vĩnh viễn", "Deleted permanently");
                case "thungrac": return UiLanguage.L("Đưa vào Thùng rác", "Sent to Recycle Bin");
                case "drive": return UiLanguage.L("Chuyển lên Google Drive", "Moved to Google Drive");
                case "ongoai": return UiLanguage.L("Chuyển sang ổ ngoài / USB", "Moved to external disk");
                case "trung": return UiLanguage.L("Xóa file trùng (Thùng rác)", "Duplicates to Recycle Bin");
                case "trinhduyet": return UiLanguage.L("Dọn trình duyệt", "Browser cleanup");
                case "smart": return UiLanguage.L("Smart Folder: sắp xếp", "Smart Folder: organized");
                case "smart_hoantac": return UiLanguage.L("Smart Folder: hoàn tác", "Smart Folder: undo");
                case "go": return UiLanguage.L("Gỡ phần mềm", "Program uninstalled");
                case "tandu": return UiLanguage.L("Dọn tàn dư phần mềm (Thùng rác)", "Program leftovers to Recycle Bin");
                case "tudon": return UiLanguage.L("Tự dọn rác theo lịch", "Scheduled junk cleanup");
                case "zalo_tudong": return UiLanguage.L("Tự đưa ảnh Zalo lên Drive", "Zalo media auto-moved to Drive");
                case "laylai": return UiLanguage.L("Lấy file về chỗ cũ", "Restored files");
                default: return k;
            }
        }

        public static void Add(string kind, int count, long bytes, IEnumerable<string> items = null, string note = "")
        {
            if (count <= 0 && bytes <= 0) return;
            var e = new HistoryEntry { Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Kind = kind, Count = count, Bytes = Math.Max(0, bytes), Note = note ?? "" };
            if (items != null) e.Items = items.Take(2000).ToList();
            lock (lk)
            {
                try
                {
                    Directory.CreateDirectory(App.DataDir);
                    File.AppendAllText(FilePath, new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(e) + Environment.NewLine, new UTF8Encoding(false));
                }
                catch { }
            }
        }

        public static List<HistoryEntry> Load(int max = 1000)
        {
            var r = new List<HistoryEntry>();
            try
            {
                if (!File.Exists(FilePath)) return r;
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                foreach (var line in File.ReadLines(FilePath))
                {
                    if (line.Length < 2) continue;
                    try { var e = js.Deserialize<HistoryEntry>(line); if (e != null) r.Add(e); } catch { }
                }
            }
            catch { }
            r.Reverse();
            return r.Take(max).ToList();
        }
    }

    // ------------------------------------------------------------------ tàn dư sau khi gỡ phần mềm
    class Leftover { public string Path, Reason; public long Size; public bool Strong; }

    static class Leftovers
    {
        static readonly HashSet<string> Generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft", "windows", "google", "temp", "tmp", "packages", "programs", "common files", "adobe", "apple", "mozilla", "intel",
            "nvidia", "amd", "realtek", "cache", "caches", "logs", "log", "data", "app", "apps", "application", "applications", "software",
            "update", "updater", "installer", "setup", "crashpad", "crashdumps", "d3dscache", "fonts", "users", "public", "default", "zalo",
            "zalopc", "zalodata", "dondepmaytinh", "oem", "drivers", "package cache", "steam", "electron", "squirreltemp", "virtualstore"
        };

        static string Norm(string s)
        {
            s = Regex.Replace(s ?? "", @"\(.*?\)|\[.*?\]", " ");
            s = Regex.Replace(s, @"\b(x64|x86|64-bit|32-bit|amd64|win64|win32)\b", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s.Trim(), @"\s+(version\s+)?v?\d+(\.\d+)*$", "", RegexOptions.IgnoreCase);
            return Regex.Replace(s.ToLowerInvariant(), @"[^\p{L}\p{N}]+", "");
        }

        static IEnumerable<string> Roots()
        {
            string pf64 = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return new[]
            {
                Fs.LocalAppData, Fs.RoamingAppData, Path.Combine(Fs.UserProfile, @"AppData\LocalLow"),
                Path.Combine(Fs.LocalAppData, "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                pf64, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            }.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// Tìm thư mục còn sót lại của phần mềm vừa gỡ. Chỉ nhận thư mục trùng đúng tên phần mềm / thư mục cài cũ,
        /// và loại bỏ những thư mục mà một phần mềm khác vẫn còn dùng.
        public static List<Leftover> Find(InstalledApp app, IEnumerable<InstalledApp> stillInstalled)
        {
            var result = new List<Leftover>();
            string name = Norm(app.Name);
            if (name.Length < 4) return result;
            var others = (stillInstalled ?? Enumerable.Empty<InstalledApp>()).Where(a => a != app).ToList();
            var otherNames = new HashSet<string>(others.Select(a => Norm(a.Name)).Where(n => n.Length > 0));
            var otherLocs = others.Select(a => a.Location).Where(l => !string.IsNullOrEmpty(l)).ToList();
            string pub = Norm(app.Publisher);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Action<string, string, bool> consider = (dir, reason, strong) =>
            {
                try
                {
                    if (!Directory.Exists(dir) || !seen.Add(dir)) return;
                    string full = Path.GetFullPath(dir).TrimEnd('\\');
                    if (full.Count(c => c == '\\') < 2) return;   // quá nông (vd. C:\Foo không phải thư mục riêng của phần mềm)
                    if (Roots().Any(r => r.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase)) ||
                        new[] { Fs.UserProfile, Fs.WinDir, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }
                            .Any(r => !string.IsNullOrEmpty(r) && (r.TrimEnd('\\').Equals(full, StringComparison.OrdinalIgnoreCase) || r.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase)))) return;
                    string folder = Path.GetFileName(dir.TrimEnd('\\'));
                    if (Generic.Contains(folder) || folder.Length < 3) return;
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) return;
                    if (PathRules.Get(dir, true).Level == Level.Cam && !strong) return;
                    if (Protect.Blocks(dir)) return;
                    if (dir.TrimEnd('\\').Equals(App.DataDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || App.AppDir.StartsWith(dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return;
                    // phần mềm khác vẫn dùng thư mục này
                    if (otherLocs.Any(l => l.StartsWith(dir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || dir.StartsWith(l.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))) return;
                    if (otherNames.Contains(Norm(folder)) && !strong) return;
                    long size = Fs.FolderSize(dir);
                    result.Add(new Leftover { Path = dir, Reason = reason, Size = size, Strong = strong });
                }
                catch { }
            };

            if (!string.IsNullOrEmpty(app.Location))
                consider(app.Location, UiLanguage.L("Thư mục cài đặt cũ", "Former install folder"), true);

            foreach (var root in Roots())
            {
                string[] subs;
                try { subs = Directory.GetDirectories(root); } catch { continue; }
                foreach (var d in subs)
                {
                    string n = Norm(Path.GetFileName(d));
                    if (n.Length == 0) continue;
                    if (n == name) consider(d, UiLanguage.L("Trùng tên phần mềm", "Matches program name"), false);
                    else if (pub.Length >= 3 && n == pub)
                    {
                        // Thư mục hãng: chỉ lấy thư mục con trùng tên phần mềm (giữ thư mục hãng cho phần mềm khác cùng hãng).
                        string[] inner;
                        try { inner = Directory.GetDirectories(d); } catch { continue; }
                        foreach (var x in inner)
                            if (Norm(Path.GetFileName(x)) == name) consider(x, UiLanguage.L("Trùng tên phần mềm (trong thư mục hãng)", "Matches program name (publisher folder)"), false);
                        bool emptyAfter = inner.All(x => Norm(Path.GetFileName(x)) == name) && SafeEmptyOfFiles(d);
                        if (emptyAfter && inner.Length > 0 && !others.Any(a => Norm(a.Publisher) == pub))
                            consider(d, UiLanguage.L("Thư mục hãng không còn phần mềm nào", "Publisher folder with no remaining programs"), false);
                    }
                }
            }
            return result.OrderByDescending(l => l.Size).ToList();
        }

        static bool SafeEmptyOfFiles(string d)
        {
            try { return !Directory.EnumerateFiles(d).Any(); } catch { return false; }
        }
    }

    // ------------------------------------------------------------------ file cài đặt thừa
    static class Installers
    {
        static readonly ISet<string> AlwaysExt = Fs.ExtSet(".msi", ".msix", ".msixbundle", ".appx", ".appxbundle", ".iso", ".img");
        static readonly Regex SetupName = new Regex(@"setup|install|installer|cài|cai[ _-]?dat|update|redist|_x64|_x86|win64|win32|\bv?\d+\.\d+", RegexOptions.IgnoreCase);

        public static List<string> Roots(Settings cfg)
        {
            var r = new List<string> { Fs.Downloads, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            r.Add(Path.Combine(docs, "Zalo Received Files"));
            r.Add(Path.Combine(Fs.RoamingAppData, @"ZaloPC\Zalo Received Files"));
            r.Add(Path.Combine(Fs.Downloads, "Telegram Desktop"));
            r.Add(Path.Combine(docs, "Telegram Desktop"));
            return r.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// File này có phải bộ cài (đã dùng xong thì xóa được) hay không.
        public static bool IsInstaller(FileInfo f, string root)
        {
            string ext = f.Extension;
            bool desktop = root.Equals(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), StringComparison.OrdinalIgnoreCase);
            if (f.FullName.Equals(App.ExePath, StringComparison.OrdinalIgnoreCase) || f.FullName.StartsWith(App.AppDir + "\\", StringComparison.OrdinalIgnoreCase)) return false;
            if (SmartFolders.IsAppFolder(f.DirectoryName)) return false;   // exe nằm cạnh .dll = phần mềm chạy trực tiếp, không phải bộ cài
            string name = Path.GetFileNameWithoutExtension(f.Name);
            if (AlwaysExt.Contains(ext)) return true;
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                return !desktop || SetupName.IsMatch(name);                // trên Desktop chỉ lấy exe có tên kiểu bộ cài
            if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) || ext.Equals(".rar", StringComparison.OrdinalIgnoreCase) || ext.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                return Regex.IsMatch(name, @"setup|install|installer|cai[ _-]?dat|portable|x64|x86|win64|win32", RegexOptions.IgnoreCase);
            return false;
        }
    }

    // ------------------------------------------------------------------ theo dõi hiệu năng
    static class SysMon
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME { public uint Low, High; public ulong V => ((ulong)High << 32) | Low; }
        [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

        static ulong lastIdle, lastTotal;
        static long lastNet; static DateTime lastNetTime;
        static readonly Dictionary<int, TimeSpan> lastCpu = new Dictionary<int, TimeSpan>();
        static DateTime lastProcTime;

        /// % CPU đang dùng (so với lần gọi trước).
        public static double Cpu()
        {
            FILETIME i, k, u;
            if (!GetSystemTimes(out i, out k, out u)) return 0;
            ulong idle = i.V, total = k.V + u.V;   // thời gian kernel đã gồm idle
            double r = 0;
            if (lastTotal > 0 && total > lastTotal) r = 100.0 * (1.0 - (double)(idle - lastIdle) / (total - lastTotal));
            lastIdle = idle; lastTotal = total;
            return Math.Max(0, Math.Min(100, r));
        }

        public static void Memory(out ulong total, out ulong avail, out int load)
        {
            var m = new MEMORYSTATUSEX();
            total = avail = 0; load = 0;
            if (GlobalMemoryStatusEx(m)) { total = m.ullTotalPhys; avail = m.ullAvailPhys; load = (int)m.dwMemoryLoad; }
        }

        /// Tốc độ mạng (byte/giây, cả gửi và nhận).
        public static double NetSpeed()
        {
            long sum = 0;
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var s = n.GetIPv4Statistics();
                    sum += s.BytesReceived + s.BytesSent;
                }
            }
            catch { }
            var now = DateTime.Now;
            double r = 0;
            if (lastNet > 0 && sum >= lastNet) r = (sum - lastNet) / Math.Max(0.5, (now - lastNetTime).TotalSeconds);
            lastNet = sum; lastNetTime = now;
            return r;
        }

        public class Proc { public int Id; public string Name; public long Ram; public double Cpu; public int Count; }

        /// Các chương trình đang chạy, gộp theo tên (Chrome nhiều tiến trình gộp làm một).
        public static List<Proc> Processes()
        {
            var now = DateTime.Now;
            double secs = lastProcTime == default(DateTime) ? 0 : (now - lastProcTime).TotalSeconds;
            int cores = Environment.ProcessorCount;
            var map = new Dictionary<string, Proc>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<int>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == 0 || p.Id == 4) continue;
                    seen.Add(p.Id);
                    long ram = p.WorkingSet64;
                    double cpu = 0;
                    try
                    {
                        var t = p.TotalProcessorTime;
                        TimeSpan old;
                        if (secs > 0 && lastCpu.TryGetValue(p.Id, out old)) cpu = 100.0 * (t - old).TotalSeconds / (secs * cores);
                        lastCpu[p.Id] = t;
                    }
                    catch { }
                    Proc g;
                    if (!map.TryGetValue(p.ProcessName, out g)) map[p.ProcessName] = g = new Proc { Id = p.Id, Name = p.ProcessName };
                    g.Ram += ram; g.Cpu += Math.Max(0, cpu); g.Count++;
                }
                catch { }
                finally { p.Dispose(); }
            }
            foreach (var id in lastCpu.Keys.Where(k => !seen.Contains(k)).ToList()) lastCpu.Remove(id);
            lastProcTime = now;
            return map.Values.ToList();
        }

        static readonly HashSet<string> Critical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Idle", "smss", "csrss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm", "explorer", "fontdrvhost",
            "Registry", "Memory Compression", "spoolsv", "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost", "sihost",
            "ctfmon", "RuntimeBroker", "MsMpEng", "NisSrv", "SecurityHealthService", "audiodg", "conhost", "taskhostw", "WmiPrvSE",
            "DonDepMayTinh", "Don Dep May Tinh", "LsaIso", "SgrmBroker", "TextInputHost", "dllhost", "smartscreen"
        };

        public static bool IsCritical(string name) { return Critical.Contains(name) || name.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase); }

        /// Đóng mọi tiến trình mang tên này (chỉ chương trình của người dùng).
        public static int Kill(string name)
        {
            if (IsCritical(name)) return 0;
            int n = 0;
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { if (p.SessionId != 0) { p.Kill(); n++; } } catch { }
                finally { p.Dispose(); }
            }
            return n;
        }

        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
        public static TimeSpan Uptime() { return TimeSpan.FromMilliseconds(GetTickCount64()); }
    }
}
