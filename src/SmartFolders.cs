// Offline Smart Folder v1.3.0: rule-based classification with preview and reversible moves.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml;

namespace DonDep
{
    internal sealed class SmartPlan
    {
        public string Source, Target, Group, Reason;
        public long Size, ModifiedTicks;
        public bool Suggested, Duplicate;
    }
    internal sealed class SmartResult
    {
        public List<SmartPlan> Items = new List<SmartPlan>();
        public int Skipped, Protected, Duplicates;
    }
    internal sealed class SmartMove
    {
        public string Id, Batch, Root, Source, Target, Type;
        public long Size, ModifiedTicks;
    }
    internal sealed class SmartOperation
    {
        public int Success, Skipped;
        public List<string> Errors = new List<string>();
        public string Batch;
    }

    internal static class SmartFolders
    {
        public const string SortedFolder = "PC Cleaner - Da sap xep";
        static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;
        static readonly string Journal = Path.Combine(App.DataDir, "smart-folder-history.jsonl");
        static readonly HashSet<string> Photos = Set(".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".tif");
        static readonly HashSet<string> Videos = Set(".mp4", ".mov", ".mkv", ".avi", ".wmv", ".webm");
        static readonly HashSet<string> Music = Set(".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg");
        static readonly HashSet<string> Docs = Set(".pdf", ".doc", ".docx", ".odt", ".txt", ".rtf");
        static readonly HashSet<string> Sheets = Set(".xls", ".xlsx", ".ods", ".csv");
        static readonly HashSet<string> Slides = Set(".ppt", ".pptx", ".odp");
        static readonly HashSet<string> Archives = Set(".zip", ".rar", ".7z", ".tar", ".gz");
        static readonly HashSet<string> Installers = Set(".msi", ".msix", ".exe");
        static readonly string[] Forbidden = { "Windows", "Program Files", "Program Files (x86)",
            "ProgramData", "AppData", "$Recycle.Bin", "System Volume Information", "Recovery",
            "Zalo Data", "ZaloData", "WindowsApps", ".git", "node_modules",
            "SteamLibrary", "steamapps", "Epic Games", "Riot Games", "Garena", "XboxGames",
            "MSOCache", "$WinREAgent", "Config.Msi", "EFI", "Boot", "PerfLogs" };
        static HashSet<string> Set(params string[] ext) { return new HashSet<string>(ext, Cmp); }
        // Preserve the trailing separator for volume roots: "D:\\" must never
        // become "D:" (a drive-relative path in Windows).
        static string Full(string p)
        {
            string full = Path.GetFullPath(p);
            string volume = Path.GetPathRoot(full);
            return Cmp.Equals(full.TrimEnd(Path.DirectorySeparatorChar),
                volume.TrimEnd(Path.DirectorySeparatorChar))
                ? volume : full.TrimEnd(Path.DirectorySeparatorChar);
        }
        internal static bool IsDriveRoot(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return false;
            string full = Full(p);
            return Regex.IsMatch(full, @"^[A-Za-z]:\\$") &&
                Cmp.Equals(full, Path.GetPathRoot(full));
        }
        static bool Inside(string root, string path)
        {
            string prefix = Full(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Full(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        static bool ForbiddenPath(string p)
        {
            var parts = Full(p).Split(Path.DirectorySeparatorChar);
            return parts.Any(s => Forbidden.Any(x => Cmp.Equals(s, x)) ||
                s.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                Cmp.Equals(s, "Google Drive") || Cmp.Equals(s, "My Drive"));
        }
        static bool LinkedDirectory(string p)
        {
            string dir = Full(p);
            while (Directory.Exists(dir))
            {
                if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0) return true;
                string parent = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || Cmp.Equals(parent, dir)) break;
                dir = parent;
            }
            return false;
        }
        public static void ValidateRoot(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new IOException("Chưa chọn thư mục.");
            string root = Full(folder);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Không thấy thư mục: " + root);
            bool driveRoot = IsDriveRoot(root);
            if (driveRoot)
            {
                // Allow a *data* drive root such as D:\, but never the Windows drive.
                // Network and other nonlocal volume roots are excluded.
                if (Cmp.Equals(root, Full(Path.GetPathRoot(Fs.WinDir))))
                    throw new IOException("Không cho phép sắp xếp toàn bộ ổ chứa Windows. Hãy chọn một thư mục riêng.");
                var drive = new DriveInfo(root);
                if (!drive.IsReady || (drive.DriveType != DriveType.Fixed &&
                    drive.DriveType != DriveType.Removable))
                    throw new IOException("Chỉ hỗ trợ ổ dữ liệu nội bộ hoặc USB đang kết nối.");
            }
            if (ForbiddenPath(root) || (!driveRoot && PathRules.IsProtected(root)) ||
                LinkedDirectory(root) || Inside(App.DataDir, root) || Inside(App.AppDir, root))
                throw new IOException("Thư mục hệ thống, Zalo, ứng dụng và đồng bộ đám mây không được hỗ trợ.");
        }
        static bool SafeFile(FileInfo f)
        {
            var a = f.Attributes;
            return (a & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Offline)) == 0 &&
                !ForbiddenPath(f.FullName) && !PathRules.IsProtected(f.FullName) &&
                !Cmp.Equals(Full(f.FullName), Full(App.ExePath)) &&
                !Inside(App.AppDir, f.FullName) && !Inside(App.DataDir, f.FullName) &&
                !LinkedDirectory(f.DirectoryName);
        }
        static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var b = new StringBuilder();
            foreach (char c in s.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    b.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            return Regex.Replace(b.ToString(), @"\s+", " ").Trim();
        }
        static string WorkGroup(string source)
        {
            string t = " " + Fold(source) + " ";
            var labels = new[] { "01 - Cong van", "02 - Quyet dinh", "03 - Ke hoach",
                "04 - Bao cao", "05 - Hop dong", "06 - Hoa don Chung tu", "07 - Ho so" };
            var rules = new[] {
                "cong van|thong bao|giay moi|thu moi",
                "quyet dinh|nghi quyet|qd so",
                "ke hoach|chuong trinh cong tac",
                "bao cao|tong ket|so ket",
                "hop dong|contract|agreement|thoa thuan",
                "hoa don|chung tu|phieu thu|phieu chi|bien lai|invoice|receipt",
                "ho so|hoc vien|tuyen sinh|visa|ho chieu|passport"
            };
            for (int i=0;i<rules.Length;i++)
                if (rules[i].Split('|').Any(x => t.Contains(" " + x + " "))) return labels[i];
            return null;
        }
        static string PersonalGroup(string ext)
        {
            if (Photos.Contains(ext)) return "01 - Hinh anh";
            if (Videos.Contains(ext)) return "02 - Video";
            if (Music.Contains(ext)) return "03 - Am thanh";
            if (Docs.Contains(ext)) return "04 - Tai lieu";
            if (Sheets.Contains(ext)) return "05 - Bang tinh";
            if (Slides.Contains(ext)) return "06 - Trinh chieu";
            if (Archives.Contains(ext)) return "07 - File nen";
            if (Installers.Contains(ext)) return "08 - Bo cai dat";
            return null;
        }
        static string ContentWords(FileInfo f)
        {
            if (f.Length > 16L * 1024 * 1024) return "";
            string ext = f.Extension.ToLowerInvariant();
            try
            {
                if (ext == ".txt" || ext == ".csv" || ext == ".rtf")
                    using (var reader = new StreamReader(f.FullName, Encoding.UTF8, true)) {
                        char[] chars = new char[20000];
                        return new string(chars, 0, reader.Read(chars, 0, chars.Length));
                    }
                if (ext != ".docx" && ext != ".xlsx" && ext != ".pptx") return "";
                using (var zip = ZipFile.OpenRead(f.FullName))
                {
                    var b = new StringBuilder();
                    var parts = zip.Entries.Where(e =>
                        (ext == ".docx" && e.FullName == "word/document.xml") ||
                        (ext == ".xlsx" && (e.FullName == "xl/sharedStrings.xml" || e.FullName.StartsWith("xl/worksheets/sheet"))) ||
                        (ext == ".pptx" && e.FullName.StartsWith("ppt/slides/slide"))).Take(3);
                    foreach (var entry in parts)
                    {
                        if (entry.Length > 512 * 1024) continue;
                        using (var stream = entry.Open())
                        using (var xml = XmlReader.Create(stream, new XmlReaderSettings {
                            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 524288 }))
                        {
                            while (xml.Read() && b.Length < 24000)
                                if (xml.NodeType == XmlNodeType.Text || xml.NodeType == XmlNodeType.CDATA)
                                    b.Append(' ').Append(xml.Value);
                        }
                    }
                    return b.ToString();
                }
            }
            catch { return ""; }
        }
        public static SmartResult Scan(string folder, bool work, bool content, bool byDate, bool checkDuplicates, Action<string> progress = null)
        {
            ValidateRoot(folder);
            string root = Full(folder), outputBase = Path.Combine(root, SortedFolder);
            var result = new SmartResult();
            var files = new List<FileInfo>();
            var stack = new Stack<string>(); stack.Push(root);
            while (stack.Count > 0 && files.Count < 20000)
            {
                string dir = stack.Pop();
                if (Cmp.Equals(dir, outputBase) || Inside(outputBase, dir)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir))
                    {
                        if (files.Count >= 20000) break;
                        try {
                            var info = new FileInfo(f);
                            if (SafeFile(info)) files.Add(info); else result.Protected++;
                        } catch { result.Skipped++; }
                    }
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                        try {
                            if (ForbiddenPath(sub) || PathRules.IsProtected(sub) || LinkedDirectory(sub) ||
                                Cmp.Equals(sub, outputBase) || Cmp.Equals(Full(sub), Full(App.AppDir)) ||
                                Cmp.Equals(Full(sub), Full(App.DataDir))) result.Protected++;
                            else stack.Push(sub);
                        } catch { result.Protected++; }
                }
                catch { result.Skipped++; }
            }
            progress?.Invoke("Đang phân loại " + files.Count + " file...");
            var duplicatePaths = new HashSet<string>(Cmp);
            if (checkDuplicates)
            {
                var signatures = new HashSet<string>(Cmp);
                foreach (var f in files.GroupBy(x => x.Length).Where(g => g.Count() > 1).SelectMany(g => g))
                    try {
                        string digest;
                        using (var sha = SHA256.Create())
                        using (var stream = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                            digest = BitConverter.ToString(sha.ComputeHash(stream));
                        if (!signatures.Add(f.Length + ":" + digest)) duplicatePaths.Add(f.FullName);
                    } catch { result.Skipped++; }
            }
            var destinations = new HashSet<string>(Cmp);
            foreach (var f in files.OrderBy(x => x.FullName, Cmp))
                try {
                    string group = work ? WorkGroup(Path.GetFileNameWithoutExtension(f.Name)) : PersonalGroup(f.Extension);
                    string reason = "Tên file / File name";
                    if (work && group == null && content)
                    {
                        group = WorkGroup(ContentWords(f));
                        if (group != null) reason = "Nội dung / Content";
                    }
                    bool duplicate = duplicatePaths.Contains(f.FullName);
                    if (duplicate) result.Duplicates++;
                    string target = group == null ? "" : Path.Combine(outputBase, group);
                    if (!string.IsNullOrEmpty(target) && byDate)
                        target = Path.Combine(target, f.LastWriteTime.ToString("yyyy"), f.LastWriteTime.ToString("MM"));
                    if (!string.IsNullOrEmpty(target)) target = Path.Combine(target, f.Name);
                    bool canMove = !duplicate && group != null && !File.Exists(target) && destinations.Add(target);
                    result.Items.Add(new SmartPlan { Source=f.FullName, Target=target, Group=group ?? "Chua xac dinh",
                        Reason=duplicate ? "File trùng SHA-256 - giữ nguyên / Duplicate" :
                            group == null ? "Chưa xác định - giữ nguyên / Unknown" :
                            !canMove ? "Trùng nơi đến - giữ nguyên / Conflict" : reason,
                        Size=f.Length, ModifiedTicks=f.LastWriteTimeUtc.Ticks,
                        Duplicate=duplicate, Suggested=canMove });
                } catch { result.Skipped++; }
            return result;
        }
        static bool CanMove(string root, SmartPlan p)
        {
            if (p == null || string.IsNullOrEmpty(p.Target) ||
                !Inside(root,p.Source) || !Inside(Path.Combine(root,SortedFolder),p.Target) ||
                ForbiddenPath(p.Source) || PathRules.IsProtected(p.Source) || PathRules.IsProtected(p.Target) ||
                LinkedDirectory(Path.GetDirectoryName(p.Source)) || LinkedDirectory(Path.GetDirectoryName(p.Target)) ||
                File.Exists(p.Target) || Directory.Exists(p.Target)) return false;
            var f = new FileInfo(p.Source);
            return f.Exists && SafeFile(f) && f.Length == p.Size && f.LastWriteTimeUtc.Ticks == p.ModifiedTicks;
        }
        static void LogMove(SmartMove m)
        {
            Directory.CreateDirectory(App.DataDir);
            File.AppendAllText(Journal, new JavaScriptSerializer().Serialize(m) + Environment.NewLine, new UTF8Encoding(false));
        }
        public static SmartOperation Apply(string root, IEnumerable<SmartPlan> selected, Action<string> progress = null)
        {
            ValidateRoot(root);
            var op = new SmartOperation { Batch = Guid.NewGuid().ToString("N") };
            foreach (var p in selected.Where(x => x.Suggested))
                try {
                    if (!CanMove(root,p)) { op.Skipped++; continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(p.Target));
                    if (!CanMove(root,p)) { op.Skipped++; continue; }
                    File.Move(p.Source,p.Target); // File.Move fails rather than overwriting.
                    try {
                        LogMove(new SmartMove { Id=Guid.NewGuid().ToString("N"), Batch=op.Batch,
                            Type="MOVE", Root=Full(root), Source=p.Source, Target=p.Target,
                            Size=p.Size, ModifiedTicks=p.ModifiedTicks });
                        op.Success++;
                    } catch {
                        if (!File.Exists(p.Source)) File.Move(p.Target,p.Source);
                        throw;
                    }
                    progress?.Invoke("Đã chuyển: " + Path.GetFileName(p.Source));
                }
                catch (Exception ex) { op.Errors.Add(Path.GetFileName(p.Source)+": "+ex.Message); }
            return op;
        }
        static List<SmartMove> History()
        {
            var all = new List<SmartMove>();
            if (!File.Exists(Journal)) return all;
            foreach(var line in File.ReadLines(Journal).Take(50000))
                try {
                    var m = new JavaScriptSerializer().Deserialize<SmartMove>(line);
                    if (m != null && !string.IsNullOrEmpty(m.Id)) all.Add(m);
                } catch { }
            return all;
        }
        static List<SmartMove> LastBatch(string root)
        {
            var log = History();
            var undone = new HashSet<string>(log.Where(x=>x.Type=="UNDO").Select(x=>x.Id), Cmp);
            var active = log.Where(x=>x.Type=="MOVE" && Cmp.Equals(x.Root,Full(root)) && !undone.Contains(x.Id)).ToList();
            if (active.Count == 0) return new List<SmartMove>();
            string batch = active[active.Count-1].Batch;
            return active.Where(x=>x.Batch==batch).Reverse().ToList();
        }
        public static int PendingUndo(string root) { return LastBatch(root).Count; }
        public static SmartOperation Undo(string root, Action<string> progress = null)
        {
            ValidateRoot(root);
            var op = new SmartOperation();
            foreach(var m in LastBatch(root))
                try {
                    if (!Inside(root,m.Source) || !Inside(Path.Combine(root,SortedFolder),m.Target) ||
                        ForbiddenPath(m.Source) || PathRules.IsProtected(m.Source) || PathRules.IsProtected(m.Target) ||
                        LinkedDirectory(Path.GetDirectoryName(m.Target)) || LinkedDirectory(Path.GetDirectoryName(m.Source)) ||
                        File.Exists(m.Source) || !File.Exists(m.Target))
                    { op.Skipped++; continue; }
                    var f = new FileInfo(m.Target);
                    if (f.Length != m.Size || f.LastWriteTimeUtc.Ticks != m.ModifiedTicks)
                    { op.Skipped++; continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(m.Source));
                    File.Move(m.Target,m.Source);
                    try {
                        LogMove(new SmartMove { Id=m.Id, Batch=m.Batch, Type="UNDO",
                            Root=m.Root, Source=m.Source, Target=m.Target });
                        op.Success++;
                    } catch {
                        if (!File.Exists(m.Target)) File.Move(m.Source,m.Target);
                        throw;
                    }
                    progress?.Invoke("Đã hoàn tác: " + Path.GetFileName(m.Source));
                }
                catch(Exception ex) { op.Errors.Add(Path.GetFileName(m.Source)+": "+ex.Message); }
            return op;
        }
        public static void SelfTest()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PC_Cleaner_SmartTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                string src = Path.Combine(root, "Quyet dinh 25.txt");
                string unknown = Path.Combine(root, "unknown.random");
                string content = Path.Combine(root, "misc.txt");
                File.WriteAllText(src, "Noi dung");
                File.WriteAllText(unknown, "Do not touch");
                File.WriteAllText(content, "BAO CAO QUY IV");
                File.WriteAllText(Path.Combine(root, "photo1.jpg"), "identical-picture-data");
                File.WriteAllText(Path.Combine(root, "photo2.jpg"), "identical-picture-data");

                // Validate volume root handling even when the CI runner has no D:.
                if (!IsDriveRoot(@"D:\") || IsDriveRoot(@"D:\MyFiles") ||
                    !Cmp.Equals(Full(@"D:\"), @"D:\") ||
                    !Inside(@"D:\", @"D:\Documents\hello.pdf") ||
                    !Cmp.Equals(Path.Combine(Full(@"D:\"), SortedFolder),
                        @"D:\" + SortedFolder))
                    throw new Exception("Smart Folder drive-root path regression.");
                // The Windows system volume itself must remain blocked.
                bool rejectedSystemDrive = false;
                try { ValidateRoot(Path.GetPathRoot(Fs.WinDir)); }
                catch (IOException) { rejectedSystemDrive = true; }
                if (!rejectedSystemDrive)
                    throw new Exception("System drive root was not blocked.");
                var personal = Scan(root,false,false,true,true);
                if(personal.Duplicates != 1 ||
                    personal.Items.Count(x=>x.Duplicate) != 1 ||
                    personal.Items.Count(x=>x.Suggested) != 3 ||
                    !personal.Items.Single(x=>x.Source.EndsWith("photo1.jpg")).Target.Contains(
                        DateTime.Now.ToString("yyyy")))
                    throw new Exception("Personal/date/duplicate classification test failed.");

                var workContent = Scan(root,true,true,false,true);
                if(workContent.Items.Single(x=>x.Source==content).Group != "04 - Bao cao")
                    throw new Exception("Offline document content classification test failed.");

                var r = Scan(root,true,false,false,true);
                if(r.Items.Count != 5 || r.Items.Count(x=>x.Suggested)!=1 ||
                    r.Items.Single(x=>x.Source==unknown).Suggested ||
                    r.Items.Single(x=>x.Source==content).Suggested)
                    throw new Exception("Smart scan test failed.");
                var item=r.Items.Single(x=>x.Suggested);
                var moved=Apply(root,new[]{item});
                if(moved.Success!=1 || !File.Exists(item.Target) || PendingUndo(root)!=1)
                    throw new Exception("Smart move/history test failed.");
                var undone=Undo(root);
                if(undone.Success!=1 || !File.Exists(src) || !File.Exists(unknown) || PendingUndo(root)!=0)
                    throw new Exception("Smart undo test failed.");
                File.WriteAllText(item.Target, "Existing destination");
                if(Apply(root,new[]{item}).Success!=0 || !File.Exists(src))
                    throw new Exception("Overwrite protection failed.");
            }
            finally { try { Directory.Delete(root,true); } catch { } }
        }
    }
}
