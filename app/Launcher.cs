using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
static class DonDepLauncher {
    [STAThread]
    static void Main(string[] args) {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(dir, "DonDep.ps1");
        if (!File.Exists(script)) { MessageBox.Show("Khong tim thay DonDep.ps1 ben canh file .exe.", "Don Dep May Tinh"); return; }
        string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        string extra = string.Join(" ", args);
        var psi = new ProcessStartInfo(ps, "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File \"" + script + "\" " + extra);
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.WorkingDirectory = dir;
        Process.Start(psi);
    }
}
