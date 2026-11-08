// Dọn Dẹp Máy Tính — xóa cookie có chọn lọc (giữ đăng nhập các trang quan trọng).
// Dùng thư viện SQLite có sẵn trong Windows 10/11 (winsqlite3.dll) — không cần cài thêm gì.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DonDep
{
    static class Sqlite
    {
        const string Dll = "winsqlite3.dll";
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr cb, IntPtr arg, out IntPtr err);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern void sqlite3_free(IntPtr p);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_changes(IntPtr db);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr tail);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern long sqlite3_column_int64(IntPtr stmt, int col);
        [DllImport(Dll, CallingConvention = CallingConvention.StdCall)] static extern int sqlite3_finalize(IntPtr stmt);

        const int OPEN_READWRITE = 2, OPEN_CREATE = 4;
        static bool? available;

        static byte[] U(string s) { return Encoding.UTF8.GetBytes(s + "\0"); }

        /// Windows này có SQLite sẵn không (Windows 10 bản 1607 trở lên).
        public static bool Available
        {
            get
            {
                if (available.HasValue) return available.Value;
                try
                {
                    IntPtr db;
                    int rc = sqlite3_open_v2(U(":memory:"), out db, OPEN_READWRITE | OPEN_CREATE, IntPtr.Zero);
                    if (db != IntPtr.Zero) sqlite3_close_v2(db);
                    available = rc == 0;
                }
                catch { available = false; }
                return available.Value;
            }
        }

        public class Db : IDisposable
        {
            IntPtr h;
            public Db(string path, bool create = false)
            {
                int rc = sqlite3_open_v2(U(path), out h, OPEN_READWRITE | (create ? OPEN_CREATE : 0), IntPtr.Zero);
                if (rc != 0) { if (h != IntPtr.Zero) sqlite3_close_v2(h); h = IntPtr.Zero; throw new IOException("SQLite open failed (" + rc + "): " + path); }
                sqlite3_busy_timeout(h, 3000);
            }
            public int Exec(string sql)
            {
                IntPtr err;
                int rc = sqlite3_exec(h, U(sql), IntPtr.Zero, IntPtr.Zero, out err);
                string msg = err != IntPtr.Zero ? Marshal.PtrToStringAnsi(err) : null;
                if (err != IntPtr.Zero) sqlite3_free(err);
                if (rc != 0) throw new IOException("SQLite error " + rc + ": " + msg);
                return sqlite3_changes(h);
            }
            public long Scalar(string sql)
            {
                IntPtr st;
                if (sqlite3_prepare_v2(h, U(sql), -1, out st, IntPtr.Zero) != 0) throw new IOException("SQLite prepare failed");
                try { return sqlite3_step(st) == 100 /* ROW */ ? sqlite3_column_int64(st, 0) : 0; }
                finally { sqlite3_finalize(st); }
            }
            public void Dispose() { if (h != IntPtr.Zero) { sqlite3_close_v2(h); h = IntPtr.Zero; } }
        }
    }

    static class CookieKeeper
    {
        public static readonly string[] Defaults =
        {
            "facebook.com", "messenger.com", "google.com", "youtube.com", "zalo.me", "chat.zalo.me",
            "live.com", "microsoftonline.com", "office.com", "github.com", "tiktok.com", "shopee.vn"
        };

        static readonly Regex DomainRx = new Regex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$");

        /// Chuẩn hóa một dòng người dùng gõ ("https://www.facebook.com/abc" → "facebook.com"). Trả về null nếu không hợp lệ.
        public static string Normalize(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"^[a-z]+://", "");
            int slash = s.IndexOfAny(new[] { '/', '?', '#', ':' });
            if (slash >= 0) s = s.Substring(0, slash);
            s = s.TrimStart('.');
            if (s.StartsWith("www.")) s = s.Substring(4);
            return DomainRx.IsMatch(s) ? s : null;
        }

        static string Where(string col, IEnumerable<string> keep)
        {
            var parts = new List<string>();
            foreach (var d in keep)
            {
                string n = Normalize(d);
                if (n == null) continue;   // đã kiểm tra: chỉ chữ, số, dấu chấm, gạch ngang — an toàn trong câu lệnh
                parts.Add(col + " = '" + n + "' OR " + col + " = '." + n + "' OR " + col + " LIKE '%." + n + "'");
            }
            return parts.Count == 0 ? "1=0" : string.Join(" OR ", parts);
        }

        /// Xóa mọi cookie, trừ cookie của các trang trong danh sách giữ. Trả về số cookie đã xóa.
        public static int DeleteExcept(string dbFile, bool firefox, IEnumerable<string> keep)
        {
            string table = firefox ? "moz_cookies" : "cookies", col = firefox ? "host" : "host_key";
            using (var db = new Sqlite.Db(dbFile))
            {
                int n = db.Exec("DELETE FROM " + table + " WHERE NOT (" + Where(col, keep) + ");");
                try { db.Exec("VACUUM;"); } catch { }
                return n;
            }
        }

        public static void SelfTest()
        {
            if (Normalize("https://www.Facebook.com/abc?x=1") != "facebook.com" || Normalize("a'b.com") != null || Normalize("localhost") != null)
                throw new Exception("Cookie domain normalize");
            if (!Sqlite.Available) return;   // Windows quá cũ: app sẽ xóa hết cookie như trước (có cảnh báo)
            string f = Path.Combine(Path.GetTempPath(), "PCCleanerCookieTest_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (var db = new Sqlite.Db(f, true))
                {
                    db.Exec("CREATE TABLE cookies (host_key TEXT, name TEXT);");
                    db.Exec("INSERT INTO cookies VALUES ('.facebook.com','c_user'),('m.facebook.com','x'),('.notfacebook.com','y'),('ads.example.com','z'),('accounts.google.com','SID');");
                }
                int del = DeleteExcept(f, false, new[] { "facebook.com", "google.com" });
                long left;
                using (var db = new Sqlite.Db(f)) left = db.Scalar("SELECT COUNT(*) FROM cookies;");
                if (del != 2 || left != 3) throw new Exception("Cookie keep test: deleted=" + del + " left=" + left);
            }
            finally { try { File.Delete(f); } catch { } }
        }
    }
}
