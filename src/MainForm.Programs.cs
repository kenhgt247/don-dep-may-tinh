// Dọn Dẹp Máy Tính — tab Gỡ phần mềm.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageBox = DonDep.LocalizedMessageBox;

namespace DonDep
{
    partial class MainForm
    {
        ListView appList;
        TextBox txtAppSearch;
        Label lblAppSum;
        List<InstalledApp> allApps = new List<InstalledApp>();
        int appSortCol = 2; bool appSortDesc = true;

        static string L(string vi, string en) { return UiLanguage.L(vi, en); }

        void BuildProgramsTab(TabPage tab)
        {
            var top = new Panel { Dock = DockStyle.Top, Height = 96 };
            var btnScan = Btn(L("Quét phần mềm đã cài", "Scan installed programs"), 8, 10, 210, 40);
            btnScan.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            var btnRemove = Btn(L("Gỡ phần mềm đã chọn", "Uninstall selected"), 226, 10, 210, 40);
            btnRemove.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnRemove.BackColor = Yellow;
            var btnSettingsApps = Btn(L("Mở Cài đặt ứng dụng của Windows", "Open Windows Apps settings"), 444, 14, 250, 32, false);
            top.Controls.Add(Lbl(L("Tìm:", "Search:"), 706, 20, 50, 22));
            txtAppSearch = new TextBox { Location = new Point(758, 17), Size = new Size(240, 24) };
            lblAppSum = Lbl(L("Bấm \"Quét phần mềm đã cài\" để xem phần mềm nào chiếm nhiều chỗ và lâu rồi không dùng.",
                              "Click \"Scan installed programs\" to see which programs use the most space and haven't been used for a long time."), 8, 60, 1150, 30);
            lblAppSum.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            top.Controls.AddRange(new Control[] { btnScan, btnRemove, btnSettingsApps, txtAppSearch, lblAppSum });

            appList = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, MultiSelect = false, HideSelection = false };
            appList.Columns.Add(L("Tên phần mềm", "Program"), 330);
            appList.Columns.Add(L("Nhà phát hành", "Publisher"), 190);
            appList.Columns.Add(L("Dung lượng", "Size"), 95, HorizontalAlignment.Right);
            appList.Columns.Add(L("Ngày cài", "Installed"), 95);
            appList.Columns.Add(L("Lần mở gần nhất (ước tính)", "Last opened (estimate)"), 175);
            appList.Columns.Add(L("Ghi chú", "Note"), 290);
            appList.ColumnClick += (s, e) =>
            {
                if (appSortCol == e.Column) appSortDesc = !appSortDesc; else { appSortCol = e.Column; appSortDesc = e.Column >= 2 && e.Column <= 4; }
                FillAppList();
            };
            appList.DoubleClick += (s, e) => { var a = SelectedApp(); if (a != null && Directory.Exists(a.Location)) Fs.OpenInExplorer(a.Location); };
            var menu = new ContextMenuStrip();
            var miRemove = menu.Items.Add(L("Gỡ phần mềm này", "Uninstall this program"));
            var miOpen = menu.Items.Add(L("Mở thư mục cài đặt", "Open install folder"));
            menu.Opening += (s, e) => { var a = SelectedApp(); if (a == null) { e.Cancel = true; return; } miOpen.Enabled = Directory.Exists(a.Location); };
            miRemove.Click += async (s, e) => await Guard(UninstallSelectedAsync);
            miOpen.Click += (s, e) => { var a = SelectedApp(); if (a != null && Directory.Exists(a.Location)) Fs.OpenInExplorer(a.Location); };
            appList.ContextMenuStrip = menu;

            tab.Controls.Add(appList);
            tab.Controls.Add(top);

            btnScan.Click += async (s, e) => await Guard(ScanAppsAsync);
            btnRemove.Click += async (s, e) => await Guard(UninstallSelectedAsync);
            btnSettingsApps.Click += (s, e) => { try { Process.Start("ms-settings:appsfeatures"); } catch { } };
            txtAppSearch.TextChanged += (s, e) => FillAppList();
        }

        InstalledApp SelectedApp() { return appList.SelectedItems.Count > 0 ? (InstalledApp)appList.SelectedItems[0].Tag : null; }

        async Task ScanAppsAsync()
        {
            SetBusy(true);
            Status(L("Đang đọc danh sách phần mềm đã cài...", "Reading installed programs..."));
            allApps = await Task.Run(() => InstalledApps.Scan(status.Report));
            FillAppList();
            Status(L("Đã tìm thấy " + allApps.Count + " phần mềm.", "Found " + allApps.Count + " programs."));
        }

        static string AppNote(InstalledApp a)
        {
            if (a.Shared) return L("⚠ Thành phần dùng chung / của hãng máy — chỉ gỡ khi chắc chắn", "⚠ Shared component / manufacturer software — remove only if sure");
            if (a.LastUsed.HasValue && (DateTime.Now - a.LastUsed.Value).TotalDays > 180) return L("💤 Hơn 6 tháng chưa mở", "💤 Not opened for 6+ months");
            if (!a.LastUsed.HasValue && a.Installed.HasValue && (DateTime.Now - a.Installed.Value).TotalDays > 180) return L("Chưa thấy mở từ Start / Desktop", "No recent launch recorded");
            return "";
        }

        void FillAppList()
        {
            if (appList == null) return;
            string q = (txtAppSearch.Text ?? "").Trim();
            IEnumerable<InstalledApp> src = allApps;
            if (q.Length > 0) src = src.Where(a => a.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || a.Publisher.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            Func<InstalledApp, IComparable> key;
            switch (appSortCol)
            {
                case 0: key = a => a.Name; break;
                case 1: key = a => a.Publisher; break;
                case 3: key = a => a.Installed ?? DateTime.MinValue; break;
                case 4: key = a => a.LastUsed ?? DateTime.MinValue; break;
                case 5: key = a => AppNote(a); break;
                default: key = a => a.Size; break;
            }
            var rows = (appSortDesc ? src.OrderByDescending(key) : src.OrderBy(key)).ToList();
            appList.BeginUpdate();
            appList.Items.Clear();
            foreach (var a in rows)
            {
                var it = new ListViewItem(a.Name + (a.Version.Length > 0 ? "  " + a.Version : ""));
                it.SubItems.Add(a.Publisher);
                it.SubItems.Add(a.Size > 0 ? Fmt.Size(a.Size) : "—");
                it.SubItems.Add(a.Installed.HasValue ? a.Installed.Value.ToString("dd/MM/yyyy") : "—");
                it.SubItems.Add(a.LastUsed.HasValue ? a.LastUsed.Value.ToString("dd/MM/yyyy") : L("Không rõ", "Unknown"));
                string note = AppNote(a);
                it.SubItems.Add(note);
                it.UseItemStyleForSubItems = false;
                if (a.Shared) it.SubItems[5].ForeColor = Color.DarkOrange;
                else if (note.Length > 0) it.SubItems[5].ForeColor = Color.SeaGreen;
                it.Tag = a;
                appList.Items.Add(it);
            }
            appList.EndUpdate();
            if (allApps.Count > 0)
            {
                long total = allApps.Sum(a => a.Size);
                int idle = allApps.Count(a => !a.Shared && a.LastUsed.HasValue && (DateTime.Now - a.LastUsed.Value).TotalDays > 180);
                lblAppSum.Text = L(allApps.Count + " phần mềm, tổng khoảng " + Fmt.Size(total) + ". " + idle + " phần mềm hơn 6 tháng chưa mở. Bấm tên cột để sắp xếp; chọn một phần mềm rồi bấm \"Gỡ phần mềm đã chọn\".",
                                   allApps.Count + " programs, about " + Fmt.Size(total) + " in total. " + idle + " not opened for 6+ months. Click a column to sort; select a program and click \"Uninstall selected\".");
            }
        }

        async Task UninstallSelectedAsync()
        {
            var a = SelectedApp();
            if (a == null) { Msg(L("Hãy chọn một phần mềm trong danh sách trước.", "Select a program in the list first.")); return; }
            string msg = L("Gỡ phần mềm \"" + a.Name + "\"" + (a.Size > 0 ? " (" + Fmt.Size(a.Size) + ")" : "") + "?\n\n" +
                           "Trình gỡ cài đặt của chính phần mềm này sẽ mở ra — hãy làm theo hướng dẫn trên màn hình. Windows có thể hỏi quyền quản trị.",
                           "Uninstall \"" + a.Name + "\"" + (a.Size > 0 ? " (" + Fmt.Size(a.Size) + ")" : "") + "?\n\n" +
                           "The program's own uninstaller will open — follow its instructions. Windows may ask for administrator permission.");
            if (!Confirm(UiLanguage.P(msg))) return;
            if (a.Shared && !Confirm(UiLanguage.P(L("\"" + a.Name + "\" là thành phần dùng chung hoặc phần mềm của hãng máy (driver, Visual C++, .NET…). Nhiều phần mềm khác có thể cần nó và có thể bị lỗi nếu gỡ.\n\nBạn vẫn muốn gỡ?",
                                                    "\"" + a.Name + "\" is a shared component or manufacturer software (driver, Visual C++, .NET…). Other programs may depend on it and could stop working.\n\nUninstall anyway?")))) return;
            SetBusy(true);
            Status(L("Đang mở trình gỡ cài đặt của " + a.Name + "...", "Opening the uninstaller for " + a.Name + "..."));
            Process p;
            try { p = InstalledApps.StartUninstall(a); }
            catch (System.ComponentModel.Win32Exception) { Status(L("Đã hủy.", "Cancelled.")); return; }   // người dùng từ chối quyền quản trị
            if (p != null)
            {
                await Task.Run(() => { try { p.WaitForExit(30 * 60 * 1000); } catch { } });
                await Task.Delay(2000);
            }
            bool still = await Task.Run(() => InstalledApps.StillInstalled(a));
            if (!still)
            {
                allApps.Remove(a);
                FillAppList();
                UpdateDrives();
                Status(L("Đã gỡ " + a.Name + ".", "Uninstalled " + a.Name + "."));
                App.Log("Đã gỡ phần mềm: " + a.Name);
            }
            else Status(L("Nếu bạn đã gỡ xong mà " + a.Name + " vẫn còn trong danh sách, bấm \"Quét phần mềm đã cài\" để làm mới.",
                          "If you finished uninstalling and " + a.Name + " is still listed, click \"Scan installed programs\" to refresh."));
        }
    }
}
