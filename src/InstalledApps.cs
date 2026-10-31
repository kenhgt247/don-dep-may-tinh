// Dọn Dẹp Máy Tính — danh sách phần mềm đã cài, ước tính lần dùng gần nhất, chạy trình gỡ cài đặt của phần mềm.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DonDep
{
    class InstalledApp
    {
        public string Name, Version, Publisher, Location, UninstallCmd, Note = "";
        public DateTime? Installed, LastUsed;
        public long Size;
        public bool Shared;       // thành phần nhiều phần mềm khác cần (Visual C++, .NET, driver…)
        public bool Msi;
    }

    static class InstalledApps
    {
        const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        static readonly Regex SharedRx = new Regex(
            @"Microsoft Visual C\+\+|\.NET|Runtime|Redistributable|Driver|Chipset|NVIDIA|Intel\(R\)|\bAMD\b|Realtek|Microsoft Edge|WebView2|" +
            @"Microsoft Update|Windows SDK|Windows Software Development|DirectX|Bonjour|\bJava\b|Microsoft 365|Microsoft Office|Office 16|Click-to-Run|" +
            @"Visual Studio|Dolby|Synaptics|ELAN|Bluetooth|Wi-?Fi|Killer|Lenovo|Dell |\bHP\b|ASUS|Acer|MSI Center",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static List<InstalledApp> Scan(Action<string> progress)
        {
            var list = new List<InstalledApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var bk = RegistryKey.OpenBaseKey(hive, view))
                        using (var k = bk.OpenSubKey(UninstallKey))
                        {
                            if (k == null) continue;
                            foreach (var sub in k.GetSubKeyNames())
                            {
                                try
                                {
                                    using (var s = k.OpenSubKey(sub))
                                    {
                                        var a = Read(s);
                                        if (a != null && seen.Add(a.Name + "|" + a.Version)) list.Add(a);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            var usage = UsageMap();
            foreach (var a in list)
            {
                progress?.Invoke("Đang xem: " + a.Name);
                if (a.Size <= 0 && !string.IsNullOrEmpty(a.Location) && Directory.Exists(a.Location) && !a.Location.StartsWith(Fs.WinDir, StringComparison.OrdinalIgnoreCase))
                    a.Size = Fs.FolderSize(a.Location);
                a.LastUsed = GuessLastUsed(a, usage);
            }
            return list;
        }

        static InstalledApp Read(RegistryKey s)
        {
            if (s == null) return null;
            string name = (s.GetValue("DisplayName") as string ?? "").Trim();
            string cmd = (s.GetValue("UninstallString") as string ?? "").Trim();
            if (name.Length == 0 || cmd.Length == 0) return null;
            if (Convert.ToInt32(s.GetValue("SystemComponent", 0)) == 1) return null;
            if (s.GetValue("ParentKeyName") != null) return null;
            string rel = s.GetValue("ReleaseType") as string ?? "";
            if (Regex.IsMatch(rel, "Update|Hotfix", RegexOptions.IgnoreCase)) return null;
            var a = new InstalledApp
            {
                Name = name,
                Version = (s.GetValue("DisplayVersion") as string ?? "").Trim(),
                Publisher = (s.GetValue("Publisher") as string ?? "").Trim(),
                UninstallCmd = cmd,
                Msi = Regex.IsMatch(cmd, @"^""?msiexec", RegexOptions.IgnoreCase)
            };
            try { a.Size = Convert.ToInt64(s.GetValue("EstimatedSize", 0)) * 1024; } catch { }
            string date = s.GetValue("InstallDate") as string;
            DateTime d;
            if (date != null && DateTime.TryParseExact(date.Trim(), "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out d)) a.Installed = d;
            a.Location = GuessLocation(s.GetValue("InstallLocation") as string, s.GetValue("DisplayIcon") as string, a.Msi ? null : cmd);
            a.Shared = SharedRx.IsMatch(name) || SharedRx.IsMatch(a.Publisher);
            return a;
        }

        static string ExeFromCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                int q = cmd.IndexOf('"', 1);
                return q > 1 ? cmd.Substring(1, q - 1) : null;
            }
            int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return i > 0 ? cmd.Substring(0, i + 4) : cmd.Split(',')[0];
        }

        static string GuessLocation(string installLocation, string icon, string uninstall)
        {
            var cands = new List<string>();
            if (!string.IsNullOrWhiteSpace(installLocation)) cands.Add(installLocation.Trim().Trim('"'));
            foreach (var c in new[] { icon, uninstall })
            {
                string exe = ExeFromCommand(c);
                if (exe == null) continue;
                exe = exe.Split(',')[0].Trim().Trim('"');
                try { cands.Add(Path.GetDirectoryName(exe)); } catch { }
            }
            foreach (var c in cands)
            {
                if (string.IsNullOrEmpty(c)) continue;
                string p;
                try { p = Path.GetFullPath(c).TrimEnd('\\'); } catch { continue; }
                // Bỏ các chỗ không phải thư mục cài thật (bộ cài tạm, Windows Installer).
                if (Regex.IsMatch(p, @"\\(Windows\\Installer|Package Cache|Temp|Windows\\System32|Windows\\SysWOW64)(\\|$)", RegexOptions.IgnoreCase)) continue;
                if (p.Length <= 3) continue;
                if (Directory.Exists(p)) return p;
            }
            return "";
        }

        // ---------------------------------------------------------- lần dùng gần nhất (ước tính)
        class Usage
        {
            public Dictionary<string, DateTime> ExePaths = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, DateTime> ExeNames = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, DateTime> Links = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        static void Max(Dictionary<string, DateTime> d, string k, DateTime t)
        {
            if (string.IsNullOrEmpty(k)) return;
            DateTime old;
            if (!d.TryGetValue(k, out old) || t > old) d[k] = t;
        }

        static string Rot13(string s)
        {
            var b = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= 'a' && c <= 'z') b.Append((char)('a' + (c - 'a' + 13) % 26));
                else if (c >= 'A' && c <= 'Z') b.Append((char)('A' + (c - 'A' + 13) % 26));
                else b.Append(c);
            }
            return b.ToString();
        }

        static Dictionary<string, string> KnownFolders()
        {
            string pf64 = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "{6D809377-6AF0-444B-8957-A3773F02200E}", pf64 },
                { "{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) },
                { "{F38BF404-1D43-42F2-9305-67DE0B28FC23}", Fs.WinDir },
                { "{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}", Environment.GetFolderPath(Environment.SpecialFolder.System) },
                { "{D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27}", Environment.GetFolderPath(Environment.SpecialFolder.SystemX86) },
                { "{F1B32785-6FBA-4FCF-9D55-7B8E7F157091}", Fs.LocalAppData },
                { "{3EB685DB-65F9-4CF6-A03A-E3EF65729F3D}", Fs.RoamingAppData },
            };
        }

        /// Windows ghi lại chương trình bạn mở từ Start / Desktop (UserAssist) và, nếu đủ quyền, trong thư mục Prefetch.
        static Usage UsageMap()
        {
            var u = new Usage();
            var kf = KnownFolders();
            try
            {
                using (var ua = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
                {
                    if (ua != null)
                        foreach (var guid in ua.GetSubKeyNames())
                            using (var cnt = ua.OpenSubKey(guid + @"\Count"))
                            {
                                if (cnt == null) continue;
                                foreach (var vn in cnt.GetValueNames())
                                {
                                    try
                                    {
                                        var data = cnt.GetValue(vn) as byte[];
                                        if (data == null || data.Length < 68) continue;
                                        long ft = BitConverter.ToInt64(data, 60);
                                        if (ft <= 0) continue;
                                        var t = DateTime.FromFileTime(ft);
                                        if (t.Year < 2005 || t > DateTime.Now.AddDays(1)) continue;
                                        string name = Rot13(vn);
                                        foreach (var kv in kf)
                                            if (name.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) { name = kv.Value + name.Substring(kv.Key.Length); break; }
                                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Max(u.ExePaths, name, t);
                                            Max(u.ExeNames, Path.GetFileName(name), t);
                                        }
                                        else if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                                            Max(u.Links, Path.GetFileNameWithoutExtension(name), t);
                                    }
                                    catch { }
                                }
                            }
                }
            }
            catch { }
            try
            {
                string pf = Path.Combine(Fs.WinDir, "Prefetch");
                foreach (var f in Directory.GetFiles(pf, "*.pf"))
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    int dash = n.LastIndexOf('-');
                    if (dash > 0) Max(u.ExeNames, n.Substring(0, dash), File.GetLastWriteTime(f));
                }
            }
            catch { }   // cần quyền quản trị — không có thì bỏ qua
            return u;
        }

        static DateTime? GuessLastUsed(InstalledApp a, Usage u)
        {
            DateTime? best = null;
            Action<DateTime> take = t => { if (best == null || t > best.Value) best = t; };
            if (!string.IsNullOrEmpty(a.Location))
            {
                string prefix = a.Location.TrimEnd('\\') + "\\";
                foreach (var kv in u.ExePaths) if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) take(kv.Value);
                try
                {
                    var exes = Directory.GetFiles(a.Location, "*.exe").Concat(
                        Directory.GetDirectories(a.Location).Take(20).SelectMany(d => { try { return Directory.GetFiles(d, "*.exe"); } catch { return new string[0]; } }));
                    foreach (var e in exes.Take(60))
                    {
                        string fn = Path.GetFileName(e);
                        if (fn.StartsWith("unins", StringComparison.OrdinalIgnoreCase) || fn.IndexOf("update", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        DateTime t;
                        if (u.ExeNames.TryGetValue(fn, out t)) take(t);
                    }
                }
                catch { }
            }
            foreach (var kv in u.Links)
                if (kv.Key.Length >= 4 && (a.Name.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith(a.Name, StringComparison.OrdinalIgnoreCase)))
                    take(kv.Value);
            return best;
        }

        // ---------------------------------------------------------- gỡ cài đặt
        /// Tách lệnh gỡ cài đặt thành file chạy + tham số. Với bộ cài MSI, đổi /I (cài / sửa) thành /X (gỡ).
        public static void SplitCommand(string cmd, out string file, out string args)
        {
            cmd = (cmd ?? "").Trim();
            var m = Regex.Match(cmd, @"^""?(?:[A-Za-z]:\\[^""]*\\)?msiexec(?:\.exe)?""?\s*(.*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                file = "msiexec.exe";
                args = Regex.Replace(m.Groups[1].Value, @"/I\s*(\{)", "/X$1", RegexOptions.IgnoreCase);
                return;
            }
            if (cmd.StartsWith("\""))
            {
                int q = cmd.IndexOf('"', 1);
                if (q > 1) { file = cmd.Substring(1, q - 1); args = cmd.Substring(q + 1).Trim(); return; }
            }
            int i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (i > 0) { file = cmd.Substring(0, i + 4); args = cmd.Substring(i + 4).Trim(); return; }
            file = cmd; args = "";
        }

        /// Mở trình gỡ cài đặt của chính phần mềm đó (người dùng làm theo hướng dẫn của nó).
        public static Process StartUninstall(InstalledApp a)
        {
            string file, args;
            SplitCommand(a.UninstallCmd, out file, out args);
            App.Log("Gỡ phần mềm: " + a.Name + " → " + file + " " + args);
            return Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true });
        }

        public static bool StillInstalled(InstalledApp a)
        {
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var bk = RegistryKey.OpenBaseKey(hive, view))
                        using (var k = bk.OpenSubKey(UninstallKey))
                        {
                            if (k == null) continue;
                            foreach (var sub in k.GetSubKeyNames())
                                using (var s = k.OpenSubKey(sub))
                                    if (s != null && string.Equals((s.GetValue("DisplayName") as string ?? "").Trim(), a.Name, StringComparison.OrdinalIgnoreCase)) return true;
                        }
                    }
                    catch { }
                }
            return false;
        }

        public static void SelfTest()
        {
            string f, a;
            SplitCommand("MsiExec.exe /I{1234-ABCD}", out f, out a);
            if (f != "msiexec.exe" || a != "/X{1234-ABCD}") throw new Exception("Uninstall MSI parse: " + f + " " + a);
            SplitCommand("\"C:\\Program Files\\App X\\uninst.exe\" /S", out f, out a);
            if (f != "C:\\Program Files\\App X\\uninst.exe" || a != "/S") throw new Exception("Uninstall quoted parse");
            SplitCommand("C:\\Apps\\Tool\\unins000.exe /SILENT", out f, out a);
            if (f != "C:\\Apps\\Tool\\unins000.exe" || a != "/SILENT") throw new Exception("Uninstall plain parse");
            if (Rot13("Mnyb.rkr") != "Zalo.exe") throw new Exception("UserAssist decode");
        }
    }
}
