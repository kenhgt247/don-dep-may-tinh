// Dọn Dẹp Máy Tính — bản đồ ổ đĩa và tìm file trùng.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace DonDep
{
    class MapNode
    {
        public string Path, Name;
        public bool IsDir;
        public long Size, Count;
        public bool Denied;
        public MapNode Parent;
        public List<MapNode> Kids = new List<MapNode>();
        public List<MapNode> BigFiles = new List<MapNode>();
        PathInfo _info;
        public PathInfo Info => _info ?? (_info = PathRules.Get(Path, IsDir));
    }

    static class DiskMap
    {
        /// Phân tích cả ổ đĩa: tính dung lượng từng thư mục (không đi theo liên kết).
        public static MapNode Scan(string root, Action<string> progress, CancellationToken ct)
        {
            var top = new MapNode { Path = root, Name = root, IsDir = true };
            var order = new List<MapNode>();
            var stack = new Stack<MapNode>();
            stack.Push(top);
            int n = 0;
            var sw = Stopwatch.StartNew();
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var node = stack.Pop();
                order.Add(node);
                var di = new DirectoryInfo(node.Path);
                try
                {
                    foreach (var f in di.EnumerateFiles())
                    {
                        long len = 0;
                        try { len = f.Length; } catch { }
                        node.Size += len; node.Count++;
                        if (len >= 20 * Fmt.MB) node.BigFiles.Add(new MapNode { Path = f.FullName, Name = f.Name, IsDir = false, Size = len, Count = 1, Parent = node });
                    }
                }
                catch { node.Denied = true; }
                try
                {
                    foreach (var s in di.EnumerateDirectories())
                    {
                        try { if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                        var kid = new MapNode { Path = s.FullName, Name = s.Name, IsDir = true, Parent = node };
                        node.Kids.Add(kid);
                        stack.Push(kid);
                    }
                }
                catch { node.Denied = true; }
                n++;
                if (n % 500 == 0) progress?.Invoke("Đang phân tích (" + n.ToString("N0") + " thư mục, " + (int)sw.Elapsed.TotalSeconds + " giây): " + node.Path);
            }
            for (int i = order.Count - 1; i >= 0; i--)
            {
                var d = order[i];
                if (d.Parent != null) { d.Parent.Size += d.Size; d.Parent.Count += d.Count; }
            }
            App.Log("Phân tích xong ổ " + root + ": " + n.ToString("N0") + " thư mục, " + Fmt.Size(top.Size) + " đọc được, mất " + (int)sw.Elapsed.TotalSeconds + " giây.");
            return top;
        }

        /// Sau khi xóa: trừ dung lượng khỏi các thư mục cha và bỏ mục đã mất.
        public static void AfterDelete(MapNode node, long remaining)
        {
            long delta = node.Size - remaining;
            if (delta <= 0) return;
            for (var p = node.Parent; p != null; p = p.Parent) p.Size -= delta;
            if (remaining <= 0)
            {
                if (node.Parent != null)
                {
                    if (node.IsDir) node.Parent.Kids.Remove(node);
                    else node.Parent.BigFiles.Remove(node);
                }
            }
            else node.Size = remaining;
        }
    }

    class DupGroup
    {
        public List<FileInfo> Files;
        public long Waste => Files.Count < 2 ? 0 : Files[0].Length * (Files.Count - 1);
    }

    static class DupFinder
    {
        static readonly string[] skip = { "AppData", "node_modules", ".git", "Windows", "Program Files*", "ProgramData", "$RECYCLE.BIN", "System Volume Information", "Zalo Data", "DonDep*" };

        public static List<DupGroup> Find(IEnumerable<string> folders, long minBytes, Action<string> progress, CancellationToken ct)
        {
            var all = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in folders)
                foreach (var f in Fs.Walk(r, skip, Math.Max(1, minBytes), null, null, progress, ct))
                    if (!all.ContainsKey(f.FullName)) all[f.FullName] = f;
            progress?.Invoke("Đã thấy " + all.Count.ToString("N0") + " file, đang so sánh...");
            var groups = new List<DupGroup>();
            var bySize = all.Values.GroupBy(f => f.Length).Where(g => g.Count() > 1).ToList();
            int done = 0;
            foreach (var g in bySize)
            {
                ct.ThrowIfCancellationRequested();
                foreach (var q in g.GroupBy(f => Hash(f.FullName, 65536)).Where(x => x.Key != null && x.Count() > 1))
                    foreach (var full in q.GroupBy(f => Hash(f.FullName, 0)).Where(x => x.Key != null && x.Count() > 1))
                        groups.Add(new DupGroup { Files = full.OrderBy(f => f.FullName.Length).ThenBy(f => f.LastWriteTime).ToList() });
                done++;
                if (done % 20 == 0) progress?.Invoke("Đang so sánh nhóm " + done + "/" + bySize.Count + "...");
            }
            return groups.OrderByDescending(x => x.Waste).ToList();
        }

        static string Hash(string path, int limit)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20))
                {
                    byte[] h;
                    if (limit > 0)
                    {
                        var buf = new byte[(int)Math.Min(limit, fs.Length)];
                        int read = fs.Read(buf, 0, buf.Length);
                        h = md5.ComputeHash(buf, 0, read);
                    }
                    else h = md5.ComputeHash(fs);
                    return BitConverter.ToString(h);
                }
            }
            catch { return null; }
        }
    }
}
