// Dọn Dẹp Máy Tính — chương trình chạy cùng Windows.
// Tắt / bật giống hệt Task Manager (ghi vào khóa StartupApproved), KHÔNG xóa gì — bật lại lúc nào cũng được.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DonDep
{
    class StartupEntry
    {
        public string Name, Command, Source, Publisher = "", Target = "";
        public bool Enabled = true, NeedsAdmin;
        public RegistryHive ApprovedHive;
        public string ApprovedSub, ApprovedValue;
        public string Hint = "";
        public bool KeepAdvised;
    }

    static class StartupItems
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        static readonly Regex Keep = new Regex(
            @"SecurityHealth|Windows Security|Windows Defender|Realtek|RtkAud|RAVCpl|igfx|Intel|NVIDIA|nvcpl|AMD|Radeon|Synaptics|ELAN|Dolby|Waves|MaxxAudio|Bluetooth|" +
            @"UniKey|EVKey|OpenKey|GoTiengViet|Vietkey|Laban ?Key|DonDepMayTinh|Don Dep May Tinh|PC Cleaner|ctfmon",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static readonly Regex Typing = new Regex(@"UniKey|EVKey|OpenKey|GoTiengViet|Vietkey|Laban ?Key", RegexOptions.IgnoreCase);
        static readonly Regex Safe = new Regex(
            @"Zalo|Teams|Skype|Spotify|Discord|Steam|EpicGames|Epic Games|Telegram|Messenger|uTorrent|BitTorrent|qBittorrent|CCleaner|Opera|Brave|Edge.*Update|" +
            @"GoogleDriveFS|Dropbox|iCloud|Adobe|Creative Cloud|CapCut|Garena|Riot|Battle\.net|EA ?Desktop|Ubisoft|WhatsApp|Viber|Line|UltraViewer|TeamViewer|AnyDesk|IDM|Internet Download Manager|Cốc Cốc|CocCoc",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static List<StartupEntry> Scan()
        {
            var list = new List<StartupEntry>();
            ReadRun(list, RegistryHive.CurrentUser, RegistryView.Default, "Run", UiLanguage.L("Tài khoản này", "This user"), false);
            ReadRun(list, RegistryHive.LocalMachine, RegistryView.Registry64, "Run", UiLanguage.L("Mọi tài khoản", "All users"), true);
            if (Environment.Is64BitOperatingSystem)
                ReadRun(list, RegistryHive.LocalMachine, RegistryView.Registry32, "Run32", UiLanguage.L("Mọi tài khoản (32-bit)", "All users (32-bit)"), true);
            ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), RegistryHive.CurrentUser, UiLanguage.L("Thư mục Khởi động", "Startup folder"), false);
            ReadFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), RegistryHive.LocalMachine, UiLanguage.L("Thư mục Khởi động (mọi tài khoản)", "Startup folder (all users)"), true);
            foreach (var e in list) Describe(e);
            return list.OrderBy(e => !e.Enabled).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static void ReadRun(List<StartupEntry> list, RegistryHive hive, RegistryView view, string approvedName, string source, bool admin)
        {
            try
            {
                using (var bk = RegistryKey.OpenBaseKey(hive, view))
                using (var k = bk.OpenSubKey(RunKey))
                {
                    if (k == null) return;
                    foreach (var v in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(v)) continue;
                        string cmd = (k.GetValue(v) ?? "").ToString();
                        var e = new StartupEntry { Name = v, Command = cmd, Source = source, NeedsAdmin = admin, ApprovedHive = hive, ApprovedSub = Approved + approvedName, ApprovedValue = v };
                        e.Target = Environment.ExpandEnvironmentVariables(InstalledApps.ExeFromCommand(cmd) ?? "");
                        e.Enabled = IsEnabled(e);
                        list.Add(e);
                    }
                }
            }
            catch { }
        }

        static void ReadFolder(List<StartupEntry> list, string dir, RegistryHive hive, string source, bool admin)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir))
                {
                    string n = Path.GetFileName(f);
                    if (n.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    var e = new StartupEntry { Name = Path.GetFileNameWithoutExtension(f), Command = f, Source = source, NeedsAdmin = admin, ApprovedHive = hive, ApprovedSub = Approved + "StartupFolder", ApprovedValue = n };
                    e.Target = f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? (LinkTarget(f) ?? f) : f;
                    e.Enabled = IsEnabled(e);
                    list.Add(e);
                }
            }
            catch { }
        }

        static string LinkTarget(string lnk)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell");
                object sh = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
                return sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
            }
            catch { return null; }
        }

        static void Describe(StartupEntry e)
        {
            try
            {
                if (!string.IsNullOrEmpty(e.Target) && File.Exists(e.Target))
                {
                    var vi = FileVersionInfo.GetVersionInfo(e.Target);
                    e.Publisher = (vi.CompanyName ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription) && vi.FileDescription.Trim().Length > 2 && e.Name.Length < 4) e.Name = vi.FileDescription.Trim();
                }
            }
            catch { }
            string all = e.Name + " " + e.Command + " " + e.Publisher;
            if (Typing.IsMatch(all)) { e.KeepAdvised = true; e.Hint = UiLanguage.L("⌨ Bộ gõ tiếng Việt — nên giữ", "⌨ Vietnamese keyboard — keep it"); }
            else if (Keep.IsMatch(all)) { e.KeepAdvised = true; e.Hint = UiLanguage.L("⚠ Driver / bảo mật / hệ thống — nên giữ", "⚠ Driver / security / system — keep it"); }
            else if (Safe.IsMatch(all)) e.Hint = UiLanguage.L("✓ Tắt được — mở tay khi cần dùng", "✓ Safe to disable — open it when needed");
            if (!string.IsNullOrEmpty(e.Target) && !File.Exists(e.Target) && Path.IsPathRooted(e.Target) && e.Hint.Length == 0)
                e.Hint = UiLanguage.L("Không thấy file — có thể phần mềm đã bị gỡ", "File missing — program may be uninstalled");
        }

        static RegistryKey OpenHive(RegistryHive h) { return RegistryKey.OpenBaseKey(h, RegistryView.Registry64); }

        static bool IsEnabled(StartupEntry e)
        {
            try
            {
                using (var bk = OpenHive(e.ApprovedHive))
                using (var k = bk.OpenSubKey(e.ApprovedSub))
                {
                    var b = k?.GetValue(e.ApprovedValue) as byte[];
                    if (b == null || b.Length == 0) return true;
                    return (b[0] & 1) == 0;   // 02/06 = bật, 03/07 = tắt (giống Task Manager)
                }
            }
            catch { return true; }
        }

        /// Bật / tắt một mục (không xóa). Mục của "mọi tài khoản" cần quyền quản trị.
        public static void SetEnabled(StartupEntry e, bool enable)
        {
            var data = new byte[12];
            data[0] = (byte)(enable ? 2 : 3);
            if (!enable) BitConverter.GetBytes(DateTime.Now.ToFileTime()).CopyTo(data, 4);
            using (var bk = OpenHive(e.ApprovedHive))
            using (var k = bk.CreateSubKey(e.ApprovedSub))
            {
                if (k == null) throw new UnauthorizedAccessException();
                k.SetValue(e.ApprovedValue, data, RegistryValueKind.Binary);
            }
            e.Enabled = enable;
            App.Log((enable ? "Bật" : "Tắt") + " chạy cùng Windows: " + e.Name + " (" + e.Command + ")");
        }

        public static void SelfTest()
        {
            // Ghi / đọc trạng thái bằng một mục giả trong khóa của tài khoản hiện tại, rồi xóa đi.
            var e = new StartupEntry { Name = "PCCleanerSelfTest", ApprovedHive = RegistryHive.CurrentUser, ApprovedSub = Approved + "Run", ApprovedValue = "PCCleanerSelfTest_" + Guid.NewGuid().ToString("N") };
            try
            {
                SetEnabled(e, false);
                if (IsEnabled(e)) throw new Exception("Startup disable not stored");
                SetEnabled(e, true);
                if (!IsEnabled(e)) throw new Exception("Startup enable not stored");
            }
            finally
            {
                try { using (var bk = OpenHive(RegistryHive.CurrentUser)) using (var k = bk.OpenSubKey(e.ApprovedSub, true)) k?.DeleteValue(e.ApprovedValue, false); } catch { }
            }
            Scan();
        }
    }
}
