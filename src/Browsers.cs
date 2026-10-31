// Dọn Dẹp Máy Tính — dọn dẹp trình duyệt: bộ nhớ đệm, lịch sử, cookie.
// Không bao giờ đụng mật khẩu đã lưu, dấu trang (bookmark), tiện ích mở rộng.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DonDep
{
    enum BrowserItemKind { Cache, History, Cookies }

    class BrowserItem
    {
        public string Browser, Profile, ProfileDir;
        public BrowserItemKind Kind;
        public List<string> Dirs = new List<string>();   // xóa nội dung bên trong
        public List<string> Files = new List<string>();  // xóa file
        public long Size;
        public string[] Processes;

        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case BrowserItemKind.Cache: return "Bộ nhớ đệm (cache)";
                    case BrowserItemKind.History: return "Lịch sử duyệt web";
                    default: return "Cookie (dữ liệu đăng nhập trang web)";
                }
            }
        }
        public string Note
        {
            get
            {
                switch (Kind)
                {
                    case BrowserItemKind.Cache: return "An toàn — trang web tự tải lại khi cần";
                    case BrowserItemKind.History: return "Mất danh sách trang đã xem và lịch sử tải về";
                    default: return "⚠ Sẽ bị ĐĂNG XUẤT khỏi Facebook, Gmail, Zalo web…";
                }
            }
        }
    }

    static class Browsers
    {
        class ChromiumDef { public string Name, UserData; public string[] Proc; }

        static List<ChromiumDef> Chromium()
        {
            string local = Fs.LocalAppData, roam = Fs.RoamingAppData;
            return new List<ChromiumDef>
            {
                new ChromiumDef { Name = "Google Chrome", UserData = Path.Combine(local, @"Google\Chrome\User Data"), Proc = new[] { "chrome" } },
                new ChromiumDef { Name = "Microsoft Edge", UserData = Path.Combine(local, @"Microsoft\Edge\User Data"), Proc = new[] { "msedge" } },
                new ChromiumDef { Name = "Cốc Cốc", UserData = Path.Combine(local, @"CocCoc\Browser\User Data"), Proc = new[] { "browser" } },
                new ChromiumDef { Name = "Brave", UserData = Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data"), Proc = new[] { "brave" } },
                new ChromiumDef { Name = "Opera", UserData = Path.Combine(roam, @"Opera Software\Opera Stable"), Proc = new[] { "opera" } },
                new ChromiumDef { Name = "Opera GX", UserData = Path.Combine(roam, @"Opera Software\Opera GX Stable"), Proc = new[] { "opera" } },
            };
        }

        static readonly string[] CacheDirs = { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache", "DawnCache", "DawnGraphiteCache", "DawnWebGPUCache", "GrShaderCache", "ShaderCache" };
        static readonly string[] HistoryFiles = { "History", "History-journal", "Visited Links", "Top Sites", "Top Sites-journal", "Shortcuts", "Shortcuts-journal", "Network Action Predictor", "Network Action Predictor-journal" };
        static readonly string[] CookieFiles = { @"Network\Cookies", @"Network\Cookies-journal", "Cookies", "Cookies-journal" };

        /// Tìm các trình duyệt và profile trên máy, tính dung lượng từng mục.
        public static List<BrowserItem> Detect()
        {
            var items = new List<BrowserItem>();
            foreach (var b in Chromium())
            {
                if (!Directory.Exists(b.UserData)) continue;
                var names = ProfileNames(b.UserData);
                var profiles = new List<string>();
                // Opera lưu thẳng trong thư mục gốc, không có "Default"
                if (b.Name.StartsWith("Opera")) profiles.Add(b.UserData);
                else
                {
                    try { profiles.AddRange(Directory.GetDirectories(b.UserData).Where(d => { string n = Path.GetFileName(d); return n == "Default" || n.StartsWith("Profile "); })); } catch { }
                }
                foreach (var prof in profiles)
                {
                    string dirName = Path.GetFileName(prof);
                    string label;
                    string def = UiLanguage.English ? "Default" : "Mặc định";
                    if (b.Name.StartsWith("Opera")) label = def;
                    else if (names.TryGetValue(dirName, out label) && !string.IsNullOrEmpty(label) && label != dirName) label = dirName + " — " + label;
                    else label = dirName == "Default" ? def : dirName;
                    // Opera để cache ở thư mục Local riêng
                    var cacheBase = new List<string> { prof };
                    if (b.Name.StartsWith("Opera")) cacheBase.Add(Path.Combine(Fs.LocalAppData, @"Opera Software\" + Path.GetFileName(prof)));
                    var cache = new BrowserItem { Browser = b.Name, Profile = label, ProfileDir = prof, Kind = BrowserItemKind.Cache, Processes = b.Proc };
                    foreach (var cb in cacheBase) foreach (var c in CacheDirs) { string p = Path.Combine(cb, c); if (Directory.Exists(p)) cache.Dirs.Add(p); }
                    var hist = new BrowserItem { Browser = b.Name, Profile = label, ProfileDir = prof, Kind = BrowserItemKind.History, Processes = b.Proc };
                    foreach (var f in HistoryFiles) { string p = Path.Combine(prof, f); if (File.Exists(p)) hist.Files.Add(p); }
                    var cook = new BrowserItem { Browser = b.Name, Profile = label, ProfileDir = prof, Kind = BrowserItemKind.Cookies, Processes = b.Proc };
                    foreach (var f in CookieFiles) { string p = Path.Combine(prof, f); if (File.Exists(p)) cook.Files.Add(p); }
                    foreach (var it in new[] { cache, hist, cook }) { Measure(it); if (it.Dirs.Count + it.Files.Count > 0) items.Add(it); }
                }
            }
            // Firefox: chỉ dọn bộ nhớ đệm và cookie (lịch sử nằm chung file với dấu trang nên không xóa)
            string ffRoam = Path.Combine(Fs.RoamingAppData, @"Mozilla\Firefox\Profiles");
            string ffLocal = Path.Combine(Fs.LocalAppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffRoam))
            {
                string[] profs = new string[0];
                try { profs = Directory.GetDirectories(ffRoam); } catch { }
                foreach (var prof in profs)
                {
                    string name = Path.GetFileName(prof);
                    string label = name.Contains(".") ? name.Substring(name.IndexOf('.') + 1) : name;
                    var cache = new BrowserItem { Browser = "Firefox", Profile = label, ProfileDir = prof, Kind = BrowserItemKind.Cache, Processes = new[] { "firefox" } };
                    foreach (var c in new[] { "cache2", "startupCache", "thumbnails" }) { string p = Path.Combine(ffLocal, name, c); if (Directory.Exists(p)) cache.Dirs.Add(p); }
                    var cook = new BrowserItem { Browser = "Firefox", Profile = label, ProfileDir = prof, Kind = BrowserItemKind.Cookies, Processes = new[] { "firefox" } };
                    foreach (var f in new[] { "cookies.sqlite", "cookies.sqlite-wal", "cookies.sqlite-shm" }) { string p = Path.Combine(prof, f); if (File.Exists(p)) cook.Files.Add(p); }
                    foreach (var it in new[] { cache, cook }) { Measure(it); if (it.Dirs.Count + it.Files.Count > 0) items.Add(it); }
                }
            }
            return items;
        }

        static void Measure(BrowserItem it)
        {
            long s = 0;
            foreach (var d in it.Dirs) s += Fs.FolderSize(d);
            foreach (var f in it.Files) { try { s += new FileInfo(f).Length; } catch { } }
            it.Size = s;
        }

        /// Tên hiển thị của từng profile (đọc từ file "Local State" của trình duyệt).
        static Dictionary<string, string> ProfileNames(string userData)
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string ls = Path.Combine(userData, "Local State");
                if (!File.Exists(ls)) return r;
                var js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var d = js.Deserialize<Dictionary<string, object>>(File.ReadAllText(ls, Encoding.UTF8));
                var profile = d["profile"] as Dictionary<string, object>;
                var cache = profile?["info_cache"] as Dictionary<string, object>;
                if (cache == null) return r;
                foreach (var kv in cache)
                {
                    var info = kv.Value as Dictionary<string, object>;
                    object n;
                    if (info != null && info.TryGetValue("name", out n) && n != null) r[kv.Key] = n.ToString();
                }
            }
            catch { }
            return r;
        }

        public static List<string> RunningBrowsers(IEnumerable<BrowserItem> items)
        {
            var names = new List<string>();
            foreach (var g in items.GroupBy(i => i.Browser))
            {
                var procs = g.First().Processes ?? new string[0];
                bool running = false;
                foreach (var p in procs) { try { if (Process.GetProcessesByName(p).Length > 0) running = true; } catch { } }
                if (running) names.Add(g.Key);
            }
            return names;
        }

        /// Xóa một mục. Trả về số byte còn lại (file đang dùng thì bị bỏ qua).
        public static long Clean(BrowserItem it)
        {
            foreach (var d in it.Dirs)
            {
                var di = new DirectoryInfo(d);
                if (!di.Exists) continue;
                try
                {
                    foreach (var f in di.GetFiles()) { try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { } }
                    foreach (var sd in di.GetDirectories()) Fs.RemoveTreeSafe(sd.FullName, DateTime.MaxValue);
                }
                catch { }
            }
            foreach (var f in it.Files)
            {
                try { if (File.Exists(f)) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); } } catch { }
            }
            long before = it.Size;
            Measure(it);
            App.Log("Dọn " + it.Browser + " (" + it.Profile + ") — " + it.KindText + ": " + Fmt.Size(Math.Max(0, before - it.Size)));
            return it.Size;
        }
    }
}
