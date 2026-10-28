// Dọn Dẹp Máy Tính — file khởi động.
// Chạy DonDep.ps1 (nằm cạnh file .exe) ngay bên trong chương trình này bằng thư viện PowerShell của Windows.
// Không mở cửa sổ powershell.exe ẩn và không đổi chính sách bảo mật của máy.
using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class DonDepLauncher
{
    [STAThread]
    static int Main(string[] args)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
        string scriptPath = Path.Combine(dir, "DonDep.ps1");
        bool selfTest = Array.Exists(args, a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
        bool tray = Array.Exists(args, a => a.Equals("-Tray", StringComparison.OrdinalIgnoreCase));

        if (!File.Exists(scriptPath))
        {
            if (!selfTest) MessageBox.Show("Không tìm thấy file DonDep.ps1 bên cạnh file .exe.\nHãy giải nén đầy đủ thư mục DonDepMayTinh rồi chạy lại.", "Dọn Dẹp Máy Tính");
            return 2;
        }

        try
        {
            string code = File.ReadAllText(scriptPath, Encoding.UTF8);
            using (Runspace rs = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault()))
            {
                rs.ApartmentState = ApartmentState.STA;
                rs.ThreadOptions = PSThreadOptions.UseCurrentThread;
                rs.Open();
                rs.SessionStateProxy.SetVariable("DonDepAppDir", dir);
                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = rs;
                    if (selfTest)
                    {
                        // Kiểm tra khi đóng gói: chạy toàn bộ mã, dựng giao diện rồi dừng (không hiện cửa sổ)
                        rs.SessionStateProxy.SetVariable("DonDepSelfTest", true);
                        ps.AddScript(code);
                        var result = ps.Invoke();
                        var sb = new StringBuilder();
                        foreach (var o in result) if (o != null) sb.Append(o.ToString()).Append(" | ");
                        foreach (var e in ps.Streams.Error) sb.Append("ERR: ").Append(e.ToString()).Append(" @ ").Append(e.InvocationInfo != null ? e.InvocationInfo.PositionMessage : "").Append(" | ");
                        File.WriteAllText(Path.Combine(dir, "selftest.txt"), sb.ToString());
                        return sb.ToString().Contains("UI_OK") && ps.Streams.Error.Count == 0 ? 0 : 1;
                    }
                    ps.AddScript(code);
                    if (tray) ps.AddParameter("Tray", true);
                    ps.Invoke();
                    if (ps.HadErrors && ps.Streams.Error.Count > 0)
                    {
                        WriteLog(ps.Streams.Error[0].ToString());
                    }
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            WriteLog(ex.ToString());
            if (!selfTest) MessageBox.Show("Không mở được Dọn Dẹp Máy Tính:\n\n" + ex.Message, "Dọn Dẹp Máy Tính");
            return 1;
        }
    }

    static void WriteLog(string text)
    {
        try
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DonDepMayTinh");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "loi_khoi_dong.txt"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, Encoding.UTF8);
        }
        catch { }
    }
}
