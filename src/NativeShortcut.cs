// Native Windows shortcut creation: avoids fragile WScript.Shell reflection / TargetInvocationException.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace DonDep
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    internal class ShellLinkComObject { }

    // IShellLinkW method order MUST match the native Windows SDK vtable.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, int flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int maxChars);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxChars);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int maxPath, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, int reserved);
        void Resolve(IntPtr window, int flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    internal static class NativeShortcut
    {
        public static void Create(string linkPath, string targetExe, string arguments, string description)
        {
            if (string.IsNullOrWhiteSpace(linkPath) || !linkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Shortcut path must end in .lnk.", "linkPath");
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
                throw new FileNotFoundException("The application executable does not exist.", targetExe);
            string folder = Path.GetDirectoryName(Path.GetFullPath(linkPath));
            if (string.IsNullOrEmpty(folder)) throw new IOException("Shortcut destination folder is unavailable.");

            // Windows users can redirect their Desktop to OneDrive. Create only the
            // requested folder; do not silently write to a different Desktop.
            Directory.CreateDirectory(folder);
            object com = null;
            try
            {
                com = new ShellLinkComObject();
                var link = (IShellLinkW)com;
                link.SetPath(targetExe);
                link.SetArguments(arguments ?? "");
                link.SetWorkingDirectory(Path.GetDirectoryName(targetExe));
                link.SetDescription(description ?? "");
                link.SetIconLocation(targetExe, 0);
                link.SetShowCmd(string.Equals(arguments, "-Tray", StringComparison.OrdinalIgnoreCase) ? 7 : 1);
                ((IPersistFile)com).Save(linkPath, true);
                if (!File.Exists(linkPath) || new FileInfo(linkPath).Length < 64)
                    throw new IOException("Windows did not save a valid shortcut file.");
            }
            catch (COMException ex)
            {
                throw new IOException("Windows Shell could not create the shortcut at: " + linkPath +
                    " (HRESULT 0x" + ex.ErrorCode.ToString("X8") + "). " + ex.Message, ex);
            }
            finally
            {
                if (com != null && Marshal.IsComObject(com)) Marshal.FinalReleaseComObject(com);
            }
        }

        // A real native .lnk smoke test on Windows CI, isolated from Desktop and Start Menu.
        public static void SelfTest()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DonDep_Shortcut_Test_" + Guid.NewGuid().ToString("N"));
            string lnk = Path.Combine(dir, "Kiem tra bieu tuong.lnk");
            try
            {
                Create(lnk, App.ExePath, "", "PC Cleaner shortcut smoke test");
                using (var stream = File.OpenRead(lnk))
                {
                    // MS-SHLLINK Shell Link Header begins 4C 00 00 00.
                    if (stream.ReadByte() != 0x4C || stream.ReadByte() != 0 ||
                        stream.ReadByte() != 0 || stream.ReadByte() != 0)
                        throw new IOException("Invalid Windows .lnk header.");
                }
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
