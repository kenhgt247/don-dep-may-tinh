// Dọn Dẹp Máy Tính — phần lõi: thiết lập, quét file, nhận biết file, nguồn rác, Zalo.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace DonDep
{
    static class App
    {
        public const string Version = "1.1.0";
        public const string Repo = "kenhgt247/don-dep-may-tinh";
        public const string Name = "Dọn Dẹp Máy Tính";
        public static string Title => Name + " " + Version + (IsAdmin ? "  —  quyền quản trị" : "");
        public static readonly string ExePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        public static readonly string AppDir = Path.GetDirectoryName(ExePath);
        public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DonDepMayTinh");
        public static string SettingsFile => Path.Combine(DataDir, "caidat.json");
        public static string RestoredFile => Path.Combine(DataDir, "da_lay_lai.txt");
        public static string LogFile => Path.Combine(DataDir, "nhatky_" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
        public static readonly string PcName = Regex.Replace(Environment.MachineName, "[^A-Za-z0-9_-]", "_");
        public const string RemoteName = "dondep";

        static bool? _admin;
        public static bool IsAdmin
        {
            get
            {
                if (_admin == null)
                {
                    try { _admin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
                    catch { _admin = false; }
                }
                return _admin.Value;
            }
        }

        public static Action<string> LogSink;
        static readonly object logLock = new object();

        public static void Log(string msg)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
            lock (logLock)
            {
                try { Directory.CreateDirectory(DataDir); File.AppendAllText(LogFile, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
            var sink = LogSink;
            if (sink != null) sink(line);
        }

        static Icon _icon;
        public static Icon AppIcon
        {
            get
            {
                if (_icon == null)
                {
                    try { _icon = Icon.ExtractAssociatedIcon(ExePath); } catch { }
                    if (_icon == null) _icon = SystemIcons.Application;
                }
                return _icon;
            }
        }
    }

    // ------------------------------------------------------------------ thiết lập (đọc được file caidat.json của bản cũ)
    class Settings
    {
        public int Days = 90, LargeMB = 500, KeepGB = 10, AlertGB = 5, AutoDays = 7, DupMinMB = 1;
        public string DestMode = "drive", DestFolder = "", LastAutoClean = "";
        public bool AutoClean = false;
        public List<string> ExtraZalo = new List<string>();
        public List<string> DupFolders = new List<string>();

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(App.SettingsFile)) return s;
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(App.SettingsFile, Encoding.UTF8));
                if (d == null) return s;
                s.Days = GetInt(d, "Days", s.Days);
                s.LargeMB = GetInt(d, "LargeMB", s.LargeMB);
                s.KeepGB = GetInt(d, "KeepGB", s.KeepGB);
                s.AlertGB = GetInt(d, "AlertGB", s.AlertGB);
                s.AutoDays = GetInt(d, "AutoDays", s.AutoDays);
                s.DupMinMB = GetInt(d, "DupMinMB", s.DupMinMB);
                s.DestMode = GetStr(d, "DestMode", s.DestMode);
                s.DestFolder = GetStr(d, "DestFolder", s.DestFolder);
                s.LastAutoClean = GetStr(d, "LastAutoClean", s.LastAutoClean);
                object b;
                if (d.TryGetValue("AutoClean", out b) && b is bool) s.AutoClean = (bool)b;
                s.ExtraZalo = GetList(d, "ExtraZalo");
                s.DupFolders = GetList(d, "DupFolders");
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(App.DataDir);
                var d = new Dictionary<string, object>
                {
                    { "Days", Days }, { "LargeMB", LargeMB }, { "KeepGB", KeepGB }, { "AlertGB", AlertGB }, { "AutoDays", AutoDays },
                    { "DupMinMB", DupMinMB }, { "DestMode", DestMode }, { "DestFolder", DestFolder }, { "LastAutoClean", LastAutoClean },
                    { "AutoClean", AutoClean }, { "ExtraZalo", ExtraZalo.ToArray() }, { "DupFolders", DupFolders.ToArray() }
                };
                File.WriteAllText(App.SettingsFile, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }

        static int GetInt(Dictionary<string, object> d, string k, int def)
        {
            object v;
            if (!d.TryGetValue(k, out v) || v == null) return def;
            try { return Convert.ToInt32(v); } catch { return def; }
        }
        static string GetStr(Dictionary<string, object> d, string k, string def)
        {
            object v;
            if (!d.TryGetValue(k, out v) || v == null) return def;
            return v.ToString();
        }
        static List<string> GetList(Dictionary<string, object> d, string k)
        {
            var r = new List<string>();
            object v;
            if (!d.TryGetValue(k, out v) || v == null) return r;
            var en = v as IEnumerable;
            if (v is string) { if (((string)v).Length > 0) r.Add((string)v); return r; }
            if (en != null) foreach (var x in en) if (x != null && x.ToString().Length > 0) r.Add(x.ToString());
            return r;
        }
    }

    static class Fmt
    {
        public static string Size(double b)
        {
            var ci = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            if (b >= 1099511627776.0) return (b / 1099511627776.0).ToString("N2", ci) + " TB";
            if (b >= 1073741824.0) return (b / 1073741824.0).ToString("N2", ci) + " GB";
            if (b >= 1048576.0) return (b / 1048576.0).ToString("N1", ci) + " MB";
            if (b >= 1024.0) return (b / 1024.0).ToString("N0", ci) + " KB";
            return b.ToString("N0", ci) + " B";
        }
        public const long MB = 1048576L, GB = 1073741824L;
    }

    // ------------------------------------------------------------------ duyệt file an toàn
    static class Fs
    {
        const FileAttributes Offline = (FileAttributes)0x1000;
        const FileAttributes RecallOnOpen = (FileAttributes)0x40000;
        const FileAttributes RecallOnData = (FileAttributes)0x400000;
        static readonly FileAttributes BadAttr = FileAttributes.System | Offline | RecallOnOpen | RecallOnData;

        /// Liệt kê file: bỏ qua lỗi quyền, không đi theo liên kết (junction), bỏ qua file hệ thống và file chỉ trên mây.
        public static List<FileInfo> Walk(string root, IEnumerable<string> skipPatterns = null, long minSize = 0,
            DateTime? before = null, ISet<string> exts = null, Action<string> progress = null, CancellationToken ct = default(CancellationToken))
        {
            var result = new List<FileInfo>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return result;
            var skips = (skipPatterns ?? Enumerable.Empty<string>()).Select(WildToRegex).ToList();
            var stack = new Stack<string>();
            stack.Push(root);
            int count = 0;
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                string dir = stack.Pop();
                var di = new DirectoryInfo(dir);
                FileInfo[] files = null;
                try { files = di.GetFiles(); } catch { }
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        try
                        {
                            if ((f.Attributes & BadAttr) != 0) continue;
                            if (f.Length < minSize) continue;
                            if (before.HasValue && f.LastWriteTime >= before.Value) continue;
                            if (exts != null && !exts.Contains(f.Extension)) continue;
                            result.Add(f);
                        }
                        catch { }
                    }
                }
                DirectoryInfo[] subs = null;
                try { subs = di.GetDirectories(); } catch { }
                if (subs != null)
                {
                    foreach (var s in subs)
                    {
                        try
                        {
                            if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                            if (skips.Any(rx => rx.IsMatch(s.Name))) continue;
                            stack.Push(s.FullName);
                        }
                        catch { }
                    }
                }
                count++;
                if (progress != null && count % 50 == 0) progress("Đang quét: " + dir);
            }
            return result;
        }

        public static long FolderSize(string path, DateTime? before = null, CancellationToken ct = default(CancellationToken))
        {
            long sum = 0;
            foreach (var f in Walk(path, null, 0, before, null, null, ct)) { try { sum += f.Length; } catch { } }
            return sum;
        }

        static Regex WildToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }

        /// Xóa cây thư mục: không đi theo liên kết, bỏ qua file còn mới hoặc đang dùng.
        public static void RemoveTreeSafe(string path, DateTime before)
        {
            var di = new DirectoryInfo(path);
            if (!di.Exists) return;
            if ((di.Attributes & FileAttributes.ReparsePoint) != 0) { try { di.Delete(); } catch { } return; }
            try
            {
                foreach (var f in di.GetFiles())
                {
                    if (f.LastWriteTime >= before) continue;
                    try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { }
                }
            }
            catch { }
            try { foreach (var d in di.GetDirectories()) RemoveTreeSafe(d.FullName, before); } catch { }
            try { di.Delete(); } catch { }
        }

        public static Dictionary<string, long> FreeMap()
        {
            var m = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in DriveInfo.GetDrives())
            {
                try { if (d.IsReady) m[d.Name.ToUpperInvariant()] = d.AvailableFreeSpace; } catch { }
            }
            return m;
        }

        public static long DriveFree(string path)
        {
            try { return new DriveInfo(Path.GetPathRoot(path)).AvailableFreeSpace; } catch { return 0; }
        }

        public static string Root(string path)
        {
            try { return (Path.GetPathRoot(path) ?? "").ToUpperInvariant(); } catch { return ""; }
        }

        public static IEnumerable<DriveInfo> FixedDrives()
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                bool ok = false;
                try { ok = d.DriveType == DriveType.Fixed && d.IsReady; } catch { }
                if (ok) yield return d;
            }
        }

        public static string SafeName(string root)
        {
            return Regex.Replace(root.TrimEnd('\\'), "[:\\\\/]+", "_").Trim('_');
        }

        public static string RelPath(string root, string path)
        {
            return path.Substring(root.TrimEnd('\\').Length).TrimStart('\\');
        }

        public static void OpenInExplorer(string path)
        {
            try
            {
                if (File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch { }
        }

        public static ISet<string> ExtSet(params string[] exts)
        {
            return new HashSet<string>(exts, StringComparer.OrdinalIgnoreCase);
        }

        // ---- thư mục đặc biệt
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);

        static string _downloads;
        public static string Downloads
        {
            get
            {
                if (_downloads != null) return _downloads;
                try
                {
                    IntPtr p;
                    if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out p) == 0)
                    {
                        _downloads = Marshal.PtrToStringUni(p);
                        Marshal.FreeCoTaskMem(p);
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(_downloads)) _downloads = Path.Combine(UserProfile, "Downloads");
                return _downloads;
            }
        }

        public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        public static string SystemDrive => Path.GetPathRoot(WinDir).TrimEnd('\\');
    }

    // ------------------------------------------------------------------ nhận biết file / thư mục
    enum Level { Cam, Rac, AnToan, CaNhan, CanThan, KhongRo }

    class PathInfo
    {
        public Level Level; public string Title; public string Text;
        public PathInfo(Level l, string t, string x) { Level = l; Title = t; Text = x; }

        public string Label
        {
            get
            {
                switch (Level)
                {
                    case Level.Cam: return "⛔ KHÔNG ĐƯỢC XÓA";
                    case Level.Rac: return "✅ RÁC — xóa an toàn";
                    case Level.AnToan: return "🟢 Thường xóa được";
                    case Level.CaNhan: return "🔵 Dữ liệu cá nhân";
                    case Level.CanThan: return "⚠️ CẨN THẬN";
                    default: return "❔ Chưa rõ là gì";
                }
            }
        }
        public string Short
        {
            get
            {
                switch (Level)
                {
                    case Level.Cam: return "Không được xóa";
                    case Level.Rac: return "Rác";
                    case Level.AnToan: return "Xóa được";
                    case Level.CaNhan: return "Cá nhân";
                    case Level.CanThan: return "Cẩn thận";
                    default: return "Chưa rõ";
                }
            }
        }
        public Color Color
        {
            get
            {
                switch (Level)
                {
                    case Level.Cam: return Color.Firebrick;
                    case Level.Rac: return Color.ForestGreen;
                    case Level.AnToan: return Color.SeaGreen;
                    case Level.CaNhan: return Color.RoyalBlue;
                    case Level.CanThan: return Color.DarkOrange;
                    default: return Color.DimGray;
                }
            }
        }
    }

    static class PathRules
    {
        class Rule { public Regex Rx; public Level L; public string T, X; }
        static readonly List<Rule> rules = new List<Rule>();
        static void R(string rx, Level l, string t, string x) { rules.Add(new Rule { Rx = new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), L = l, T = t, X = x }); }

        static PathRules()
        {
            R(@"^[a-z]:\\?$", Level.Cam, "Cả ổ đĩa", "Đây là gốc của ổ đĩa. Không xóa — hãy bấm đúp để vào bên trong.");
            R(@"^[a-z]:\\Windows\\SoftwareDistribution\\Download(\\|$)", Level.Rac, "Gói cập nhật Windows đã tải", "Windows tải bản cập nhật về đây; cài xong thì xóa được. Cần bấm \"Quyền admin\". Cách dễ hơn: tab Dọn dẹp → Rác hệ thống.");
            R(@"^[a-z]:\\Windows\\Temp(\\|$)", Level.Rac, "File tạm của Windows", "File tạm do Windows và trình cài đặt tạo ra. Xóa an toàn (file đang dùng sẽ tự được bỏ qua).");
            R(@"^[a-z]:\\Windows\.old(\\|$)", Level.Cam, "Bản Windows cũ (Windows.old)", "Bản Windows trước lần nâng cấp, thường 10–30 GB. Không xóa được bằng cách thông thường. Cách xóa đúng: bấm Start, gõ \"Disk Cleanup\" → chọn ổ C → \"Clean up system files\" → tích \"Previous Windows installation(s)\" → OK.");
            R(@"\\\$Recycle\.Bin(\\|$)", Level.Cam, "Thùng rác", "Đừng xóa trực tiếp ở đây. Hãy dọn bằng mục \"Thùng rác\" trong tab Dọn dẹp, hoặc chuột phải Thùng rác trên Desktop → Empty Recycle Bin.");
            R(@"^[a-z]:\\hiberfil\.sys$", Level.Cam, "File ngủ đông (Hibernate)", "Windows dùng file này khi máy \"ngủ đông\". Không xóa trực tiếp được. Nếu bạn không dùng ngủ đông: bấm \"Quyền admin\", quét lại ở tab Dọn dẹp rồi chọn \"Tắt chế độ ngủ đông\".");
            R(@"^[a-z]:\\(pagefile|swapfile)\.sys$", Level.Cam, "Bộ nhớ ảo của Windows", "Windows dùng file này như RAM phụ. Không được xóa — máy sẽ chậm hoặc treo.");
            R(@"^[a-z]:\\(System Volume Information|Recovery|Boot|EFI|\$WinREAgent|\$Windows\.~BT|\$Windows\.~WS|Config\.Msi|MSOCache)(\\|$)", Level.Cam, "Dữ liệu hệ thống / khôi phục", "Windows dùng để khởi động và khôi phục máy. Không được xóa.");
            R(@"\\AppData\\Local\\Temp(\\|$)", Level.Rac, "File tạm", "Các phần mềm để file tạm ở đây. Xóa an toàn (file đang dùng sẽ tự được bỏ qua).");
            R(@"\\AppData\\.*\\(Cache|Code Cache|GPUCache|cache2|CacheStorage|ShaderCache|DawnCache|GrShaderCache|CrashDumps|npm-cache)(\\|$)", Level.Rac, "Bộ nhớ đệm", "Bản lưu tạm giúp phần mềm chạy nhanh hơn; xóa thì phần mềm tự tạo lại. Nên tắt phần mềm đó trước khi xóa.");
            R(@"\\Zalo ?Data\\(media\\)?update(\\|$)", Level.Rac, "Gói cập nhật Zalo đã tải", "Zalo tải bản cập nhật về đây; cài xong thì xóa được.");
            R(@"\\Zalo ?Data\\(Cache|Code Cache|GPUCache|Partitions\\zalo\\(Cache|Code Cache|GPUCache))(\\|$)", Level.Rac, "Bộ nhớ đệm Zalo", "Bản lưu tạm của Zalo; xóa thì Zalo tự tải lại. Nên tắt Zalo trước.");
            R(@"\\Zalo ?Data\\message(\\|$)", Level.Cam, "Tin nhắn Zalo", "Toàn bộ lịch sử tin nhắn Zalo trên máy này. Xóa là mất tin nhắn.");
            R(@"\\Zalo ?Data$", Level.Cam, "Dữ liệu Zalo", "Thư mục gốc chứa tin nhắn và ảnh Zalo. Không xóa cả thư mục — hãy vào bên trong (media → ... → ZaloDownloads) để chọn ảnh, video.");
            R(@"\\Zalo ?Data\\media(\\|$)", Level.CanThan, "Ảnh, video, file Zalo", "File nhận qua Zalo. Xóa thì Zalo không mở lại được các file đó trong cuộc trò chuyện. Nên dùng tab Dọn dẹp → \"Ảnh / video / file Zalo cũ\" để chuyển lên Drive thay vì xóa.");
            R(@"^[a-z]:\\Windows(\\|$)", Level.Cam, "Thư mục Windows", "Hệ điều hành Windows. Xóa bất cứ thứ gì ở đây có thể làm máy không khởi động được.");
            R(@"^[a-z]:\\Program Files", Level.Cam, "Phần mềm đã cài", "Các phần mềm đã cài. Đừng xóa thư mục — muốn bỏ phần mềm thì vào Settings → Apps → Installed apps → Uninstall.");
            R(@"^[a-z]:\\ProgramData(\\|$)", Level.Cam, "Dữ liệu chung của phần mềm", "Windows và các phần mềm lưu dữ liệu dùng chung ở đây. Xóa có thể làm phần mềm lỗi.");
            R(@"\\AppData\\Local\\Programs(\\|$)", Level.Cam, "Phần mềm đã cài (cho riêng bạn)", "Phần mềm cài cho tài khoản này (ví dụ Zalo, VS Code). Muốn bỏ thì Settings → Apps → Uninstall, đừng xóa thư mục.");
            R(@"\\AppData\\Local\\Packages(\\|$)", Level.Cam, "Ứng dụng Microsoft Store", "Dữ liệu của các ứng dụng cài từ Microsoft Store. Muốn bỏ thì gỡ ứng dụng trong Settings → Apps.");
            R(@"\\AppData$|\\AppData\\(Local|Roaming|LocalLow)$", Level.Cam, "Dữ liệu ứng dụng", "Nơi mọi phần mềm lưu cài đặt và dữ liệu. Không xóa cả thư mục — hãy vào bên trong để xem từng phần mềm.");
            R(@"\\AppData\\", Level.CanThan, "Dữ liệu của một phần mềm", "Cài đặt và dữ liệu của phần mềm (có thể gồm tin nhắn, mật khẩu đã lưu, lịch sử). Xóa có thể làm phần mềm mất dữ liệu hoặc bị lỗi. Chỉ nên xóa nếu bạn đã gỡ phần mềm này.");
            R(@"^[a-z]:\\Users$", Level.Cam, "Tài khoản người dùng", "Chứa dữ liệu của mọi tài khoản trên máy. Không xóa — hãy vào bên trong.");
            R(@"^[a-z]:\\Users\\(Default|Public|All Users|Default User)$", Level.Cam, "Tài khoản hệ thống", "Windows dùng thư mục này. Không xóa.");
            R(@"^[a-z]:\\Users\\[^\\]+$", Level.Cam, "Thư mục tài khoản của bạn", "Chứa toàn bộ dữ liệu và cài đặt của tài khoản. Không xóa cả thư mục — hãy vào bên trong.");
            R(@"\\DonDepMayTinh(\\|$)", Level.Cam, "Dữ liệu của Dọn Dẹp Máy Tính", "Thiết lập, nhật ký và danh sách file đã chuyển của app này. Không xóa.");
            R(@"\\node_modules(\\|$)", Level.AnToan, "Thư viện lập trình (node_modules)", "Thư viện tải về cho dự án lập trình; xóa được, khi cần chạy \"npm install\" để tải lại.");
            R(@"\\(\.Ld\d*VirtualBox|LDPlayer\d*|BlueStacks[^\\]*|Nox[^\\]*|VirtualBox VMs|MEmu[^\\]*)(\\|$)", Level.CanThan, "Máy ảo / giả lập Android", "Dữ liệu của phần mềm giả lập (LDPlayer, BlueStacks, Nox...) hoặc máy ảo. Xóa là mất game, ứng dụng và dữ liệu trong máy ảo đó. Muốn bỏ hẳn thì gỡ phần mềm giả lập trong Settings → Apps.");
            R(@"\\(steamapps|SteamLibrary|Epic Games|Riot Games|Garena|XboxGames)(\\|$)", Level.CanThan, "Game", "Game đã cài. Muốn bỏ thì gỡ trong Steam / Epic / Garena hoặc Settings → Apps, đừng xóa thư mục.");
        }

        static List<string> _personal;
        static List<string> Personal
        {
            get
            {
                if (_personal == null)
                {
                    var l = new List<string>();
                    foreach (var f in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.MyMusic })
                    {
                        try { var p = Environment.GetFolderPath(f); if (!string.IsNullOrEmpty(p)) l.Add(p); } catch { }
                    }
                    l.Add(Fs.Downloads);
                    l.Add(Path.Combine(Fs.UserProfile, "OneDrive"));
                    _personal = l;
                }
                return _personal;
            }
        }

        static readonly ISet<string> vdisk = Fs.ExtSet(".vhd", ".vhdx", ".vdi", ".vmdk", ".qcow2");
        static readonly ISet<string> installer = Fs.ExtSet(".exe", ".msi", ".msix", ".appx");
        static readonly ISet<string> program = Fs.ExtSet(".exe", ".dll", ".sys");
        static readonly ISet<string> iso = Fs.ExtSet(".iso", ".img");
        static readonly ISet<string> temp = Fs.ExtSet(".tmp", ".dmp", ".log", ".old", ".bak", ".crdownload", ".part");
        static readonly ISet<string> archive = Fs.ExtSet(".zip", ".rar", ".7z");

        public static PathInfo Get(string path, bool isDir)
        {
            string p = path;
            if (p.Length > 3) p = p.TrimEnd('\\');
            foreach (var r in rules) if (r.Rx.IsMatch(p)) return new PathInfo(r.L, r.T, r.X);
            string dl = Fs.Downloads;
            if (!isDir)
            {
                string ext = Path.GetExtension(p);
                bool inDl = !string.IsNullOrEmpty(dl) && p.StartsWith(dl, StringComparison.OrdinalIgnoreCase);
                if (vdisk.Contains(ext)) return new PathInfo(Level.CanThan, "Ổ đĩa ảo", "Ổ cứng của một máy ảo hoặc phần mềm giả lập (LDPlayer, BlueStacks, VirtualBox...). Xóa là mất toàn bộ máy ảo đó.");
                if (ext.Equals(".pst", StringComparison.OrdinalIgnoreCase) || ext.Equals(".ost", StringComparison.OrdinalIgnoreCase)) return new PathInfo(Level.CanThan, "Hộp thư Outlook", "Email lưu trên máy của Outlook. Xóa là có thể mất email cũ.");
                if (installer.Contains(ext) && (inDl || Regex.IsMatch(p, "setup|install", RegexOptions.IgnoreCase))) return new PathInfo(Level.AnToan, "File cài đặt phần mềm", "Dùng để cài phần mềm. Nếu đã cài xong thì xóa được (cần thì tải lại).");
                if (program.Contains(ext)) return new PathInfo(Level.CanThan, "Chương trình", "File chạy của một phần mềm. Xóa sẽ làm phần mềm đó không chạy được.");
                if (iso.Contains(ext)) return new PathInfo(Level.AnToan, "File ảnh đĩa (ISO)", "Thường là bộ cài Windows hoặc phần mềm. Không cần nữa thì xóa được.");
                if (temp.Contains(ext)) return new PathInfo(Level.AnToan, "File tạm / nhật ký / bản sao cũ", "Thường không còn cần nữa. Xóa được.");
                if (inDl && archive.Contains(ext)) return new PathInfo(Level.CaNhan, "File nén đã tải về", "Nếu đã giải nén và dùng xong thì xóa được. Không chắc thì chuyển lên Drive.");
            }
            foreach (var pp in Personal)
                if (p.StartsWith(pp, StringComparison.OrdinalIgnoreCase))
                    return new PathInfo(Level.CaNhan, "Dữ liệu cá nhân của bạn", "Ảnh, video, tài liệu... của bạn. Xóa vĩnh viễn là mất hẳn. Nếu còn cần, hãy dùng \"Chuyển lên Drive / ổ ngoài\" thay vì xóa.");
            if (!p.StartsWith(Fs.SystemDrive, StringComparison.OrdinalIgnoreCase))
                return new PathInfo(Level.CaNhan, "Dữ liệu trên ổ phụ", "Thường là dữ liệu bạn tự lưu (ảnh, video, tài liệu, bộ cài...). Kiểm tra kỹ trước khi xóa; nếu còn cần thì chuyển lên Drive / ổ ngoài.");
            return new PathInfo(Level.KhongRo, "Chưa rõ là gì", "App chưa nhận ra mục này. Nếu bạn không chắc chắn, đừng xóa — hoặc chỉ xóa vào Thùng rác để còn khôi phục được. Có thể tìm tên thư mục trên Google để biết của phần mềm nào.");
        }

        static readonly Regex[] protectedRx = new[]
        {
            @"^[a-z]:\\Windows(\\|$)", @"^[a-z]:\\Program Files", @"^[a-z]:\\ProgramData(\\|$)", @"\\AppData(\\|$)",
            @"^[a-z]:\\\$Recycle\.Bin", @"^[a-z]:\\System Volume Information", @"^[a-z]:\\Recovery(\\|$)",
            @"^[a-z]:\\(pagefile|hiberfil|swapfile)\.sys$", @"\\Zalo Data\\message(\\|$)", @"\\Zalo Data$", @"\\Zalo Data\\media$", @"\\DonDepMayTinh(\\|$)"
        }.Select(s => new Regex(s, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();

        /// Đường dẫn không bao giờ được chuyển đi (chương trình, hệ thống, dữ liệu tin nhắn Zalo, dữ liệu app).
        public static bool IsProtected(string path)
        {
            string p = path.TrimEnd('\\');
            if (p.Length <= 3) return true;
            return protectedRx.Any(r => r.IsMatch(p));
        }
    }

    // ------------------------------------------------------------------ nguồn rác
    class JunkSource
    {
        public string Name, Path; public DateTime? Before; public bool Default = true;
    }

    static class Junk
    {
        public static List<string> ZaloDataBases(Settings cfg)
        {
            var cands = new List<string> { Path.Combine(Fs.RoamingAppData, "ZaloData", "media") };
            foreach (var d in Fs.FixedDrives()) cands.Add(Path.Combine(d.RootDirectory.FullName, "Zalo Data", "media"));
            foreach (var x in cfg.ExtraZalo) cands.Add(Path.Combine(x, "media"));
            return cands.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<JunkSource> Sources(Settings cfg)
        {
            var items = new List<JunkSource>();
            var dayAgo = DateTime.Now.AddDays(-1);
            var weekAgo = DateTime.Now.AddDays(-7);
            Action<string, string, DateTime?> add = (n, p, b) =>
            {
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) items.Add(new JunkSource { Name = n, Path = p, Before = b });
            };
            string local = Fs.LocalAppData, roam = Fs.RoamingAppData;
            add("File tạm của người dùng", Path.GetTempPath().TrimEnd('\\'), dayAgo);
            add("File tạm của Windows", Path.Combine(Fs.WinDir, "Temp"), dayAgo);
            add("Báo cáo lỗi Windows (người dùng)", Path.Combine(local, @"Microsoft\Windows\WER"), null);
            add("File lỗi chương trình (CrashDumps)", Path.Combine(local, "CrashDumps"), null);

            var browsers = new[] {
                new[] { "Chrome", Path.Combine(local, @"Google\Chrome\User Data") },
                new[] { "Edge", Path.Combine(local, @"Microsoft\Edge\User Data") },
                new[] { "Cốc Cốc", Path.Combine(local, @"CocCoc\Browser\User Data") },
                new[] { "Brave", Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data") } };
            foreach (var b in browsers)
            {
                if (!Directory.Exists(b[1])) continue;
                string[] profs = new string[0];
                try { profs = Directory.GetDirectories(b[1]); } catch { }
                foreach (var prof in profs)
                {
                    string pn = Path.GetFileName(prof);
                    if (pn != "Default" && !pn.StartsWith("Profile ")) continue;
                    foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage" })
                        add("Bộ nhớ đệm " + b[0] + " (" + pn + ", " + sub + ")", Path.Combine(prof, sub), null);
                }
            }
            foreach (var op in new[] { "Opera Stable", "Opera GX Stable" })
                add("Bộ nhớ đệm Opera (" + op + ")", Path.Combine(local, @"Opera Software\" + op + @"\Cache"), null);
            string ff = Path.Combine(local, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ff))
            {
                string[] profs = new string[0];
                try { profs = Directory.GetDirectories(ff); } catch { }
                foreach (var prof in profs) add("Bộ nhớ đệm Firefox (" + Path.GetFileName(prof) + ")", Path.Combine(prof, "cache2"), null);
            }
            foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage" })
                add("Bộ nhớ đệm Teams (" + sub + ")", Path.Combine(roam, @"Microsoft\Teams\" + sub), null);
            add("Bộ nhớ đệm npm (lập trình)", Path.Combine(local, "npm-cache"), null);
            add("Bộ nhớ đệm pip (lập trình)", Path.Combine(local, @"pip\cache"), null);

            string zd = Path.Combine(roam, "ZaloData");
            foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", @"Partitions\zalo\Cache", @"Partitions\zalo\Code Cache", @"Partitions\zalo\GPUCache" })
                add("Bộ nhớ đệm Zalo (" + sub + ")", Path.Combine(zd, sub), null);
            foreach (var zb in ZaloDataBases(cfg)) add("Gói cập nhật Zalo đã tải (" + zb + ")", Path.Combine(zb, "update"), weekAgo);
            string zp = Path.Combine(local, @"Programs\Zalo");
            if (Directory.Exists(zp))
            {
                var vers = new List<Tuple<Version, string>>();
                try
                {
                    foreach (var d in Directory.GetDirectories(zp))
                    {
                        var m = Regex.Match(Path.GetFileName(d), @"^Zalo-([\d\.]+)");
                        Version v;
                        if (m.Success && Version.TryParse(m.Groups[1].Value.Trim('.'), out v)) vers.Add(Tuple.Create(v, d));
                    }
                }
                catch { }
                foreach (var old in vers.OrderByDescending(t => t.Item1).Skip(1))
                {
                    add("Bản Zalo cũ không dùng nữa (" + Path.GetFileName(old.Item2) + ")", old.Item2, null);
                    if (items.Count > 0 && items[items.Count - 1].Path == old.Item2) items[items.Count - 1].Default = false;
                }
            }
            if (App.IsAdmin)
            {
                add("Gói cập nhật Windows đã cài xong", Path.Combine(Fs.WinDir, @"SoftwareDistribution\Download"), weekAgo);
                add("Bộ nhớ đệm phân phối cập nhật", Path.Combine(Fs.WinDir, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"), null);
                add("Báo cáo lỗi Windows (hệ thống)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\WER"), null);
            }
            return items;
        }

        public static long RecycleBinSize()
        {
            long sum = 0;
            string sid;
            try { sid = WindowsIdentity.GetCurrent().User.Value; } catch { return 0; }
            foreach (var d in Fs.FixedDrives())
            {
                string p = Path.Combine(d.RootDirectory.FullName, "$Recycle.Bin", sid);
                if (Directory.Exists(p)) sum += Fs.FolderSize(p);
            }
            return sum;
        }

        public static long HiberSize()
        {
            try
            {
                var f = new FileInfo(Path.Combine(Fs.SystemDrive + "\\", "hiberfil.sys"));
                if (f.Exists) return f.Length;
            }
            catch { }
            return 0;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);

        public const string RecycleBin = "THUNG_RAC";
        public const string Hibernate = "NGU_DONG";

        public static void Remove(string path, string name, DateTime? before)
        {
            if (path == RecycleBin)
            {
                try { SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4); } catch (Exception ex) { App.Log("Không dọn được thùng rác: " + ex.Message); }
                return;
            }
            if (path == Hibernate)
            {
                try
                {
                    var psi = new ProcessStartInfo("powercfg.exe", "/hibernate off") { UseShellExecute = false, CreateNoWindow = true };
                    using (var p = Process.Start(psi)) { p.WaitForExit(30000); App.Log("Tắt ngủ đông: mã kết quả " + p.ExitCode + ". Có thể bật lại bằng lệnh \"powercfg /hibernate on\"."); }
                }
                catch (Exception ex) { App.Log("Không tắt được ngủ đông: " + ex.Message); }
                return;
            }
            string pth = path.TrimEnd('\\');
            var protectedDirs = new[] { Fs.SystemDrive, Fs.UserProfile, Fs.WinDir, Fs.LocalAppData, Fs.RoamingAppData, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
            if (protectedDirs.Any(x => !string.IsNullOrEmpty(x) && x.TrimEnd('\\').Equals(pth, StringComparison.OrdinalIgnoreCase)) || pth.Length < 8)
            {
                App.Log("Bỏ qua thư mục được bảo vệ: " + pth);
                return;
            }
            DateTime b = before ?? DateTime.MaxValue;
            if (name != null && name.StartsWith("Bản Zalo cũ")) { Fs.RemoveTreeSafe(pth, b); return; }
            var di = new DirectoryInfo(pth);
            if (!di.Exists) return;
            try
            {
                foreach (var f in di.GetFiles())
                {
                    if (f.LastWriteTime >= b) continue;
                    try { f.Attributes = FileAttributes.Normal; f.Delete(); } catch { }
                }
            }
            catch { }
            try { foreach (var d in di.GetDirectories()) Fs.RemoveTreeSafe(d.FullName, b); } catch { }
        }

        public class ZaloRoot { public string Path; public ISet<string> Ext; }

        public static readonly ISet<string> ZaloExt = Fs.ExtSet(".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic",
            ".mp4", ".mov", ".avi", ".mkv", ".3gp", ".webm", ".m4v", ".mp3", ".m4a", ".aac", ".wav", ".amr", ".ogg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar", ".7z", ".psd", ".apk");

        public static List<ZaloRoot> ZaloRoots(Settings cfg)
        {
            var r = new List<ZaloRoot>();
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            r.Add(new ZaloRoot { Path = Path.Combine(docs, "Zalo Received Files") });
            r.Add(new ZaloRoot { Path = Path.Combine(Fs.RoamingAppData, @"ZaloPC\Zalo Received Files") });
            string zaloPc = Path.Combine(Fs.LocalAppData, "ZaloPC");
            var bases = new List<string>();
            if (Directory.Exists(zaloPc)) bases.Add(zaloPc);
            bases.AddRange(ZaloDataBases(cfg));
            foreach (var b in bases)
            {
                string[] accs = new string[0];
                try { accs = Directory.GetDirectories(b); } catch { }
                foreach (var acc in accs)
                {
                    string dl = Path.Combine(acc, "ZaloDownloads");
                    if (Directory.Exists(dl)) r.Add(new ZaloRoot { Path = dl, Ext = ZaloExt });
                }
            }
            foreach (var x in cfg.ExtraZalo)
                if (!Directory.Exists(Path.Combine(x, "media"))) r.Add(new ZaloRoot { Path = x, Ext = ZaloExt });
            return r;
        }

        public static bool ZaloRunning()
        {
            try { return Process.GetProcessesByName("Zalo").Length > 0; } catch { return false; }
        }
    }
}
